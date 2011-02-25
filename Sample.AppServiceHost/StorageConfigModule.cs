﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autofac;
using CommonDomain.Core;
using CommonDomain.Persistence.EventStore;
using Sample.AppService;
using CommonDomain;
using CommonDomain.Persistence;
using EventStore;
using EventStore.Persistence;
using EventStore.Persistence.SqlPersistence;
using EventStore.Persistence.SqlPersistence.SqlDialects;
using EventStore.Serialization;
using EventStore.Dispatcher;
using NanoMessageBus.Core;

namespace Sample.AppServiceHost
{
    class StorageConfigModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(c => BuildEventStore(c.Resolve<ILifetimeScope>()))
                .As<IStoreEvents>()
                .SingleInstance();

            builder.RegisterType<ConflictDetector>().As<IDetectConflicts>();
            builder.RegisterType<EventStoreRepository>().As<IRepository>();
            builder.RegisterType<AggregateFactory>().As<IConstructAggregates>();
        }

        private static IStoreEvents BuildEventStore(ILifetimeScope container)
        {
            var persistence = BuildPersistenceEngine();
            var dispatcher = BuildDispatcher(persistence, container);
            return new OptimisticEventStore(persistence, dispatcher);
        }
        private static IPersistStreams BuildPersistenceEngine()
        {
            return new SqlPersistenceEngine(
                new ConfigurationConnectionFactory("EventStore"),
                new MySqlDialect(),
                BuildSerializer());
        }
        private static ISerialize BuildSerializer()
        {
            var serializer = new JsonSerializer() as ISerialize;
            serializer = new GzipSerializer(serializer);
            return serializer;
        }

        private static IDispatchCommits BuildDispatcher(IPersistStreams persistence, ILifetimeScope container)
        {
            return new AsynchronousDispatcher(
                new DelegateMessagePublisher(c => DispatchCommit(container, c)),
                persistence,
                OnDispatchError);
        }

        private static void DispatchCommit(ILifetimeScope container, Commit commit)
        {
            using (var scope = container.BeginLifetimeScope())
            {
                NanoMessageBus.IPublishMessages publisher = scope.Resolve<NanoMessageBus.IPublishMessages>();

                publisher.Publish(commit.Events.Select(e => e.Body).ToArray());

                var uow = scope.Resolve<IHandleUnitOfWork>();
                uow.Complete();
                uow.Dispose();
            }
        }

        private static void OnDispatchError(Commit commit, Exception exception)
        {

        }
    }
}