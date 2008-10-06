﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Castle.Core;
using Castle.MicroKernel;

namespace IoC.Framework.Tests.Adapters {
    public class CastleAdapter : FrameworkAdapterBase {
        private readonly IKernel kernel = new DefaultKernel();

        public override void AddSingleton(Type serviceType, Type componentType, string key) {
            this.Register(key, serviceType, componentType, LifestyleType.Singleton);
        }

        public override void AddTransient(Type serviceType, Type componentType, string key) {
            this.Register(key, serviceType, componentType, LifestyleType.Transient);
        }

        public override void AddInstance(Type serviceType, object instance, string key) {
            key = key ?? string.Format("{0} ({1})", serviceType, instance);
            kernel.AddComponentInstance(key, serviceType, instance);
        }
        
        private void Register(string key, Type serviceType, Type componentType, LifestyleType lifestyle) {
            key = key ?? string.Format("{0} ({1})", serviceType, componentType);
            kernel.AddComponent(key, serviceType, componentType, lifestyle);
        }

        protected override object DoGetInstance(Type serviceType, string key) {
            if (key == null)
                return kernel.Resolve(serviceType);

            return kernel.Resolve(key, serviceType);
        }

        protected override IEnumerable<object> DoGetAllInstances(Type serviceType) {
            // Major pain 
            Func<object> resolve = kernel.ResolveServices<object>;
            var typed = resolve.Method.GetGenericMethodDefinition().MakeGenericMethod(serviceType);

            return (object[])typed.Invoke(kernel, null);
        }
    }
}
