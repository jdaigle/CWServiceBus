using System;
using System.Collections.Generic;
using System.Linq;
using StructureMap;

namespace CWServiceBus.StructureMap {
    public sealed class StructureMapServiceLocator : IServiceLocator {

        private readonly IContainer container;

        public StructureMapServiceLocator(IContainer container) {
            this.container = container;
        }

        public void BuildUp(object target) {
            this.container.BuildUp(target);
        }

        public T Get<T>() {
            return this.container.GetInstance<T>();
        }

        public IEnumerable<T> GetAll<T>() {
            return this.container.GetAllInstances<T>();
        }

        public object Get(Type type) {
            return this.container.GetInstance(type);
        }

        public IEnumerable<object> GetAll(Type type) {
            return this.container.GetAllInstances(type).Cast<object>();
        }

        public void RegisterComponent<T>(T instance) {
            this.container.Configure(i => {
                i.For<T>().Use(instance);
                i.FillAllPropertiesOfType<T>();
            });
        }

        public void RegisterComponent(Type type, object instance) {
            this.container.Configure(i => {
                i.For(type).Use(instance);
                i.SetAllProperties(y => y.TypeMatches(p => p == type));
            });
        }

        public IServiceLocator GetChildServiceLocator() {
            return new StructureMapServiceLocator(this.container.GetNestedContainer());
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing) {
            if (disposing) {
                this.container.Dispose();
            }
        }
    }
}
