/* This code is derived from NServiceBus 2.0 
 * https://github.com/NServiceBus/NServiceBus/blob/2.0/src/impl/messageInterfaces/NServiceBus.MessageInterfaces.MessageMapper.Reflection/MessageMapper.cs
 * 
 * Which is licensed under Apache Licence, Version 2.0
 * http://www.apache.org/licenses/LICENSE-2.0
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using log4net;

namespace CWServiceBus.Reflection {
    /// <summary>
    /// Uses reflection to map between interfaces and their generated concrete implementations.
    /// </summary>
    public class MessageMapper : IMessageMapper {
        public void SetMessageTypeConventions(MessageTypeConventions messageTypeCoventions) {
            this.messageTypeCoventions = messageTypeCoventions;
        }

        /// <summary>
        /// Scans the given types generating concrete classes for interfaces.
        /// </summary>
        /// <param name="types"></param>
        public void Initialize(IEnumerable<Type> types) {
            if (types == null || types.Count() == 0)
                return;

            string name = types.First().Namespace + SUFFIX;

            AssemblyBuilder assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(
                new AssemblyName(name),
                AssemblyBuilderAccess.Run
                );

            ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule(name);

            foreach (Type t in types)
                InitType(t, moduleBuilder);
        }

        /// <summary>
        /// Generates a concrete implementation of the given type if it is an interface.
        /// </summary>
        /// <param name="t"></param>
        /// <param name="moduleBuilder"></param>
        public void InitType(Type t, ModuleBuilder moduleBuilder) {
            if (t == null)
                return;

            if (t.IsSimpleType())
                return;

            if (typeof(IEnumerable).IsAssignableFrom(t)) {
                InitType(t.GetElementType(), moduleBuilder);

                foreach (var interfaceType in t.GetInterfaces()) {
                    foreach (var g in interfaceType.GetGenericArguments())
                        InitType(g, moduleBuilder);

                    continue;
                }

                return;
            }

            var typeName = GetTypeName(t);

            //already handled this type, prevent infinite recursion
            if (nameToType.ContainsKey(typeName))
                return;

            if (t.IsInterface) {
                GenerateImplementationFor(t, moduleBuilder);
            } else
                typeToConstructor[t] = t.GetConstructor(Type.EmptyTypes);

            nameToType[typeName] = t;

            foreach (FieldInfo field in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
                InitType(field.FieldType, moduleBuilder);

            foreach (PropertyInfo prop in t.GetProperties())
                InitType(prop.PropertyType, moduleBuilder);
        }

        private static string GetTypeName(Type t) {
            var args = t.GetGenericArguments();
            if (args.Length == 2)
                if (typeof(KeyValuePair<,>).MakeGenericType(args) == t)
                    return t.SerializationFriendlyName();

            return t.FullName;
        }

        void GenerateImplementationFor(Type interfaceType, ModuleBuilder moduleBuilder) {
            if (interfaceType.GetMethods().Any(mi => !(mi.IsSpecialName && (mi.Name.StartsWith("set_") || mi.Name.StartsWith("get_"))))) {
                Logger.Warn(string.Format("Interface {0} contains methods and can there for not be mapped. Be aware that non mapped interface can't be used to send messages.", interfaceType.Name));
                return;
            }
            var mapped = CreateTypeFrom(interfaceType, moduleBuilder);
            interfaceToConcreteTypeMapping[interfaceType] = mapped;
            concreteToInterfaceTypeMapping[mapped] = interfaceType;
            typeToConstructor[mapped] = mapped.GetConstructor(Type.EmptyTypes);
        }



        /// <summary>
        /// Generates a new full name for a type to be generated for the given type.
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public string GetNewTypeName(Type t) {
            return t.FullName + SUFFIX;
        }

        /// <summary>
        /// Generates the concrete implementation of the given type.
        /// Only properties on the given type are generated in the concrete implementation.
        /// </summary>
        /// <param name="t"></param>
        /// <param name="moduleBuilder"></param>
        /// <returns></returns>
        public Type CreateTypeFrom(Type t, ModuleBuilder moduleBuilder) {
            TypeBuilder typeBuilder = moduleBuilder.DefineType(
                GetNewTypeName(t),
                TypeAttributes.Serializable | TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed,
                typeof(object)
                );

            typeBuilder.DefineDefaultConstructor(MethodAttributes.Public);

            foreach (PropertyInfo prop in GetAllProperties(t)) {
                Type propertyType = prop.PropertyType;

                FieldBuilder fieldBuilder = typeBuilder.DefineField(
                    "field_" + prop.Name,
                    propertyType,
                    FieldAttributes.Private);

                PropertyBuilder propBuilder = typeBuilder.DefineProperty(
                    prop.Name,
                    prop.Attributes | PropertyAttributes.HasDefault,
                    propertyType,
                    null);

                MethodBuilder getMethodBuilder = typeBuilder.DefineMethod(
                    "get_" + prop.Name,
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Final | MethodAttributes.Virtual | MethodAttributes.VtableLayoutMask,
                    propertyType,
                    Type.EmptyTypes);

                ILGenerator getIL = getMethodBuilder.GetILGenerator();
                // For an instance property, argument zero is the instance. Load the 
                // instance, then load the private field and return, leaving the
                // field value on the stack.
                getIL.Emit(OpCodes.Ldarg_0);
                getIL.Emit(OpCodes.Ldfld, fieldBuilder);
                getIL.Emit(OpCodes.Ret);

                // Define the "set" accessor method for Number, which has no return
                // type and takes one argument of type int (Int32).
                MethodBuilder setMethodBuilder = typeBuilder.DefineMethod(
                    "set_" + prop.Name,
                    getMethodBuilder.Attributes,
                    null,
                    new Type[] { propertyType });

                ILGenerator setIL = setMethodBuilder.GetILGenerator();
                // Load the instance and then the numeric argument, then store the
                // argument in the field.
                setIL.Emit(OpCodes.Ldarg_0);
                setIL.Emit(OpCodes.Ldarg_1);
                setIL.Emit(OpCodes.Stfld, fieldBuilder);
                setIL.Emit(OpCodes.Ret);

                // Last, map the "get" and "set" accessor methods to the 
                // PropertyBuilder. The property is now complete. 
                propBuilder.SetGetMethod(getMethodBuilder);
                propBuilder.SetSetMethod(setMethodBuilder);
            }

            typeBuilder.AddInterfaceImplementation(t);

            return typeBuilder.CreateType();
        }

        /// <summary>
        /// Returns all properties on the given type, going up the inheritance
        /// hierarchy.
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        private static IEnumerable<PropertyInfo> GetAllProperties(Type t) {
            var properties = new List<PropertyInfo>(t.GetProperties());
            foreach (Type interfaceType in t.GetInterfaces())
                properties.AddRange(GetAllProperties(interfaceType));

            var names = new List<string>(properties.Count);
            var dups = new List<PropertyInfo>(properties.Count);
            foreach (var property in properties) {
                if (names.Contains(property.Name))
                    dups.Add(property);
                else
                    names.Add(property.Name);
            }
            foreach (var property in dups) {
                properties.Remove(property);
            }
            return properties;
        }

        /// <summary>
        /// If the given type is concrete, returns the interface it was generated to support.
        /// If the given type is an interface, returns the concrete class generated to implement it.
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public Type GetMappedTypeFor(Type t) {
            if (t.IsClass) {
                Type result;
                concreteToInterfaceTypeMapping.TryGetValue(t, out result);
                if (result != null)
                    return result;

                return t;
            }

            Type toReturn = null;
            interfaceToConcreteTypeMapping.TryGetValue(t, out toReturn);

            return toReturn;
        }

        /// <summary>
        /// Returns the type mapped to the given name.
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns></returns>
        public Type GetMappedTypeFor(string typeName) {
            if (nameToType.ContainsKey(typeName))
                return nameToType[typeName];

            return Type.GetType(typeName);
        }

        /// <summary>
        /// Calls the generic CreateInstance and performs the given
        /// action on the result.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="action"></param>
        /// <returns></returns>
        public T CreateInstance<T>(Action<T> action) {
            T result = CreateInstance<T>();
            if (action != null)
                action(result);

            return result;
        }

        /// <summary>
        /// Calls the non-generic CreateInstance and returns its result
        /// cast to T.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T CreateInstance<T>() {
            return (T)CreateInstance(typeof(T));
        }

        /// <summary>
        /// If the given type is an interface, finds its generated concrete
        /// implementation, instantiates it, and returns the result.
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public object CreateInstance(Type t) {
            Type mapped = t;
            if (t.IsInterface || t.IsAbstract) {
                mapped = GetMappedTypeFor(t);
                if (mapped == null)
                    throw new ArgumentException("Could not find a concrete type mapped to " + t.FullName);
            }

            ConstructorInfo constructor = null;
            typeToConstructor.TryGetValue(mapped, out constructor);
            if (constructor != null)
                return constructor.Invoke(null);

            return FormatterServices.GetUninitializedObject(mapped);
        }

        private MessageTypeConventions messageTypeCoventions = MessageTypeConventions.Default;

        public bool IsMessageType(Type type) {
            return messageTypeCoventions.IsMessageType(type);
        }

        public IEnumerable<Type> DynamicTypes {
            get { return concreteToInterfaceTypeMapping.Keys; }
        }

        private static readonly string SUFFIX = "__Impl";
        private static readonly Dictionary<Type, Type> interfaceToConcreteTypeMapping = new Dictionary<Type, Type>();
        private static readonly Dictionary<Type, Type> concreteToInterfaceTypeMapping = new Dictionary<Type, Type>();
        private static readonly Dictionary<string, Type> nameToType = new Dictionary<string, Type>();
        private static readonly Dictionary<Type, ConstructorInfo> typeToConstructor = new Dictionary<Type, ConstructorInfo>();
        private static ILog Logger = LogManager.GetLogger(typeof(MessageMapper).Namespace);
    }
}