using System;
using System.Reflection;

namespace pitTeam.Compat
{
    internal sealed class NativeUiMemberAccessor
    {
        private readonly PropertyInfo _property;
        private readonly Type _declaringType;
        private readonly Type _valueType;
        private readonly bool _isStatic;

        private NativeUiMemberAccessor(PropertyInfo property, bool isStatic)
        {
            _property = property;
            _declaringType = property.DeclaringType;
            _valueType = property.PropertyType;
            _isStatic = isStatic;
        }

        public static NativeUiMemberAccessor? TryForProperty(Type declaringType, string name, Type valueType, bool isStatic, bool requireSetter)
        {
            if (declaringType == null || string.IsNullOrWhiteSpace(name) || valueType == null) return null;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            PropertyInfo property;
            try { property = declaringType.GetProperty(name, flags); }
            catch (AmbiguousMatchException) { return null; }
            if (property == null || property.PropertyType != valueType) return null;
            MethodInfo getter = property.GetGetMethod(true);
            MethodInfo setter = property.GetSetMethod(true);
            if (getter == null || getter.IsStatic != isStatic) return null;
            if (setter != null && setter.IsStatic != isStatic) return null;
            if (requireSetter && setter == null) return null;
            if (setter != null)
            {
                ParameterInfo[] parameters = setter.GetParameters();
                if (parameters.Length != 1 || parameters[0].ParameterType != valueType) return null;
            }
            return new NativeUiMemberAccessor(property, isStatic);
        }

        public static NativeUiMemberAccessor ForProperty(Type declaringType, string name, Type valueType, bool isStatic, bool requireSetter)
        {
            if (declaringType == null) throw new ArgumentNullException(nameof(declaringType));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Property name is required.", nameof(name));
            if (valueType == null) throw new ArgumentNullException(nameof(valueType));

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            PropertyInfo property = declaringType.GetProperty(name, flags);
            if (property == null)
                throw new MissingMemberException(declaringType.FullName, name);
            if (property.PropertyType != valueType)
                throw new InvalidOperationException("Property type mismatch for " + declaringType.FullName + "." + name + ": expected " + valueType.FullName + ", got " + property.PropertyType.FullName + ".");

            MethodInfo getter = property.GetGetMethod(true);
            MethodInfo setter = property.GetSetMethod(true);
            if (getter == null || getter.IsStatic != isStatic)
                throw new InvalidOperationException("Property getter kind mismatch for " + declaringType.FullName + "." + name + ".");
            if (setter != null && setter.IsStatic != isStatic)
                throw new InvalidOperationException("Property setter kind mismatch for " + declaringType.FullName + "." + name + ".");
            if (requireSetter && setter == null)
                throw new InvalidOperationException("Writable property required for " + declaringType.FullName + "." + name + ".");
            if (setter != null)
            {
                ParameterInfo[] parameters = setter.GetParameters();
                if (parameters.Length != 1 || parameters[0].ParameterType != valueType)
                    throw new InvalidOperationException("Property setter signature mismatch for " + declaringType.FullName + "." + name + ".");
            }

            return new NativeUiMemberAccessor(property, isStatic);
        }

        public object GetValue(object target)
        {
            ValidateTarget(target);
            return _property.GetValue(_isStatic ? null : target);
        }

        public void SetValue(object target, object value)
        {
            ValidateTarget(target);
            MethodInfo setter = _property.GetSetMethod(true);
            if (setter == null) throw new InvalidOperationException("Property is read-only: " + _declaringType.FullName + "." + _property.Name + ".");
            if (value == null)
            {
                if (_valueType.IsValueType && Nullable.GetUnderlyingType(_valueType) == null)
                    throw new ArgumentNullException(nameof(value));
            }
            else if (!_valueType.IsInstanceOfType(value))
            {
                throw new ArgumentException("Value type mismatch for " + _declaringType.FullName + "." + _property.Name + ".", nameof(value));
            }
            _property.SetValue(_isStatic ? null : target, value);
        }

        private void ValidateTarget(object target)
        {
            if (_isStatic)
            {
                if (target != null) throw new ArgumentException("Static property access does not take an instance target.", nameof(target));
                return;
            }
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (!_declaringType.IsInstanceOfType(target))
                throw new ArgumentException("Target must be an instance of " + _declaringType.FullName + ".", nameof(target));
        }
    }
}
