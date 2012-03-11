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

        public void Inject<T>(T instance) {
            this.container.Inject<T>(instance);
        }

        public void Inject(Type type, object instance) {
            this.container.Inject(type, instance);
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
