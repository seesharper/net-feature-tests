﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StructureMap;
using StructureMap.Configuration.DSL;
using FeatureTests.On.DependencyInjection.Adapters.Interface;

namespace FeatureTests.On.DependencyInjection.Adapters {
    public class StructureMapAdapter : AdapterBase {
        private Registry initialRegistry = new Registry();
        private Container container;

        public override Assembly Assembly {
            get { return typeof(Container).Assembly; }
        }

        public override void RegisterSingleton(Type serviceType, Type implementationType) {
            this.RegisterInitialOrConfigure(
                r => r.For(serviceType).Singleton().Use(implementationType)
            );
        }

        public override void RegisterTransient(Type serviceType, Type implementationType) {
            this.RegisterInitialOrConfigure(
                r => r.For(serviceType).LifecycleIs(InstanceScope.Transient).Use(implementationType)
            );
        }

        public override void RegisterInstance(Type serviceType, object instance) {
            this.RegisterInitialOrConfigure(
                r => r.For(serviceType).LifecycleIs(InstanceScope.Transient).Use(instance)
            );
        }

        private void RegisterInitialOrConfigure(Action<Registry> register) {
            var registry = this.initialRegistry ?? new Registry();
            register(registry);

            if (this.container != null)
                this.container.Configure(c => c.AddRegistry(registry));
        }
        
        public override object Resolve(Type serviceType) {
            this.EnsureContainer();
            return this.container.GetInstance(serviceType);
        }

        public override IEnumerable<object> ResolveAll(Type serviceType) {
            this.EnsureContainer();
            return this.container.GetAllInstances(serviceType).Cast<object>();
        }

        private void EnsureContainer() {
            if (this.container != null)
                return;

            this.container = new Container(this.initialRegistry);
            this.initialRegistry = null;
        }
    }
}
