﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Streamiz.Kafka.Net.Crosscutting;

namespace Streamiz.Kafka.Net.Processors.Internal
{
    internal class TaskManager
    {
        [ThreadStatic] 
        private static StreamTask _currentTask;
        internal static StreamTask CurrentTask
        {
            get
            {
                if (_currentTask == null)
                    return UnassignedStreamTask.Create();
                return _currentTask;
            }
            set
            {
                _currentTask = value;
            }
        }

        private readonly ILogger log = Logger.GetLogger(typeof(TaskManager));
        private readonly InternalTopologyBuilder builder;
        private readonly TaskCreator taskCreator;
        private readonly IAdminClient adminClient;
        private readonly IChangelogReader changelogReader;
        private readonly ConcurrentDictionary<TopicPartition, TaskId> partitionsToTaskId = new ConcurrentDictionary<TopicPartition, TaskId>();
        private readonly ConcurrentDictionary<TaskId, StreamTask> activeTasks = new ConcurrentDictionary<TaskId, StreamTask>();
        private readonly ConcurrentDictionary<TaskId, StreamTask> revokedTasks = new ConcurrentDictionary<TaskId, StreamTask>();

        public IEnumerable<StreamTask> ActiveTasks => activeTasks.Values.ToList();
        public IEnumerable<StreamTask> RevokedTasks => revokedTasks.Values.ToList();
        public IDictionary<TaskId, ITask> Tasks => activeTasks.ToDictionary(i => i.Key, i => (ITask)i.Value);

        public IConsumer<byte[], byte[]> Consumer { get; internal set; }
        public IEnumerable<TaskId> ActiveTaskIds => activeTasks.Keys;
        public IEnumerable<TaskId> RevokeTaskIds => revokedTasks.Keys;
        public bool RebalanceInProgress { get; internal set; }

        internal TaskManager(InternalTopologyBuilder builder, TaskCreator taskCreator, IAdminClient adminClient, IChangelogReader changelogReader)
        {
            this.builder = builder;
            this.taskCreator = taskCreator;
            this.adminClient = adminClient;
            this.changelogReader = changelogReader;
        }

        internal TaskManager(InternalTopologyBuilder builder, TaskCreator taskCreator, IAdminClient adminClient, IConsumer<byte[], byte[]>  consumer, IChangelogReader changelogReader)
            : this(builder, taskCreator, adminClient, changelogReader)
        {
            Consumer = consumer;
        }


        public void CreateTasks(ICollection<TopicPartition> assignment)
        {
            CurrentTask = null;
            IDictionary<TaskId, IList<TopicPartition>> tasksToBeCreated = new Dictionary<TaskId, IList<TopicPartition>>();

            foreach (var partition in new List<TopicPartition>(assignment))
            {
                var taskId = builder.GetTaskIdFromPartition(partition);
                if (revokedTasks.ContainsKey(taskId))
                {
                    var t = revokedTasks[taskId];
                    t.Resume();
                    activeTasks.TryAdd(taskId, t);
                    revokedTasks.TryRemove(taskId, out StreamTask removeTask);
                    partitionsToTaskId.TryAdd(partition, taskId);
                }
                else if (!activeTasks.ContainsKey(taskId))
                {
                    if (tasksToBeCreated.ContainsKey(taskId))
                        tasksToBeCreated[taskId].Add(partition);
                    else
                        tasksToBeCreated.Add(taskId, new List<TopicPartition> { partition });
                    partitionsToTaskId.TryAdd(partition, taskId);
                }
            }

            if (tasksToBeCreated.Count > 0)
            {
                var tasks = taskCreator.CreateTasks(Consumer, tasksToBeCreated);
                foreach (var task in tasks)
                {
                    task.GroupMetadata = Consumer.ConsumerGroupMetadata;
                    task.InitializeStateStores();
                    task.InitializeTopology();
                    activeTasks.TryAdd(task.Id, task);
                }
            }
        }

        public void RevokeTasks(ICollection<TopicPartition> assignment)
        {
            CurrentTask = null;
            foreach (var p in assignment)
            {
                var taskId = builder.GetTaskIdFromPartition(p);
                if (activeTasks.ContainsKey(taskId))
                {
                    var task = activeTasks[taskId];
                    task.Suspend();
                    task.MayWriteCheckpoint(true);
                    if (!revokedTasks.ContainsKey(taskId))
                    {
                        revokedTasks.TryAdd(taskId, task);
                    }
                    
                    partitionsToTaskId.TryRemove(p, out TaskId removeId);
                    activeTasks.TryRemove(taskId, out StreamTask removeTask);
                }
            }
        }

        public StreamTask ActiveTaskFor(TopicPartition partition)
        {
            if (partitionsToTaskId.ContainsKey(partition))
            {
                return activeTasks[partitionsToTaskId[partition]];
            }

            return null;
        }

        public void Close()
        {
            CurrentTask = null;
            foreach (var t in activeTasks)
            {
                CurrentTask = t.Value;
                t.Value.MayWriteCheckpoint(true);
                t.Value.Close();
            }

            activeTasks.Clear();
            CurrentTask = null;

            foreach (var t in revokedTasks)
            {
                t.Value.MayWriteCheckpoint(true);
                t.Value.Close();
            }

            revokedTasks.Clear();
            partitionsToTaskId.Clear();
        }

        // NOT AVAILABLE NOW, NEED PROCESSOR API
        //internal int MaybeCommitPerUserRequested()
        //{
        //    int committed = 0;
        //    Exception firstException = null;

        //    foreach(var task in ActiveTasks)
        //    {
        //        if(task.CommitNeeded && task.CommitRequested)
        //        {
        //            try
        //            {
        //                task.Commit();
        //                ++committed;
        //                log.Debug($"Committed stream task {task.Id} per user request in");
        //            }
        //            catch(Exception e)
        //            {
        //                log.Error($"Failed to commit stream task {task.Id} due to the following error: {e}");
        //                if (firstException == null)
        //                {
        //                    firstException = e;
        //                }
        //            }
        //        }
        //    }

        //    if (firstException != null)
        //    {
        //        throw firstException;
        //    }

        //    return committed;
        //}

        internal int CommitAll()
        {
            int committed = 0;
            if (RebalanceInProgress)
            {
                return -1;
            }

            foreach (var t in ActiveTasks)
            {
                CurrentTask = t;
                if (t.CommitNeeded)
                {
                    t.Commit();
                    t.MayWriteCheckpoint();
                    ++committed;
                }
            }
            CurrentTask = null;
            return committed;
        }

        internal int Process(long now)
        {
            int processed = 0;

            foreach (var task in ActiveTasks)
            {
                try
                {
                    CurrentTask = task;
                    if (task.CanProcess(now) && task.Process())
                    {
                        processed++;
                    }
                }
                catch(Exception e)
                {
                    log.LogError(
                        e, "Failed to process stream task {TasksId} due to the following error:", task.Id);
                    throw;
                }
            }
            CurrentTask = null;
            return processed;
        }

        internal void HandleLostAll()
        {
            log.LogDebug("Closing lost active tasks as zombies");
            CurrentTask = null;
            revokedTasks.Clear();

            var enumerator = activeTasks.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var task = enumerator.Current.Value;
                task.Suspend();
                foreach(var part in task.Partition)
                {
                    partitionsToTaskId.Remove(part, out TaskId taskId);
                }
                task.Close();
                task.MayWriteCheckpoint(true);
            }
            activeTasks.Clear();
        }

        internal bool NeedRestoration()
            => ActiveTasks.Any(t => t.State == TaskState.CREATED || t.State == TaskState.RESTORING);

        internal bool TryToCompleteRestoration()
        {
            bool allRunning = true;

            foreach(var task in ActiveTasks)
            {
                try
                {
                    task.RestorationIfNeeded();
                }catch(Exception e)
                {
                    log.LogDebug($"Could not initialize task {task.Id} at {DateTime.Now.GetMilliseconds()}, will retry ({e.Message})");
                    allRunning = false;
                }
            }

            var activeTasksWithStateStore = ActiveTasks.Where(t => t.HasStateStores);
            if (allRunning && activeTasksWithStateStore.Any())
            {
                var restored = changelogReader.CompletedChangelogs;
                foreach(var task in activeTasksWithStateStore)
                {
                    if (restored.Any() && task.ChangelogPartitions.ContainsAll(restored))
                        task.CompleteRestoration();
                    else
                        allRunning = false;
                }
            }

            if (allRunning)
                Consumer.Resume(Consumer.Assignment);

            return allRunning;
        }
    }
}