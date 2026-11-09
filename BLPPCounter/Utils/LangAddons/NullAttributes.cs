namespace System.Diagnostics.CodeAnalysis
{
    /*/// <summary>
    /// Specifies that when a method returns <see cref="ReturnValue"/>,
    /// the parameter will not be null even if the corresponding type allows it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class NotNullWhenAttribute : Attribute
    {
        /// <summary>
        /// Initializes the attribute with the specified return value condition.
        /// </summary>
        /// <param name="returnValue">
        /// The return value condition. If the method returns this value,
        /// the associated parameter will not be null.
        /// </param>
        public NotNullWhenAttribute(bool returnValue)
        {
            ReturnValue = returnValue;
        }

        /// <summary>
        /// Gets the return value condition.
        /// </summary>
        public bool ReturnValue { get; }
    }*/

    /// <summary>
    /// Specifies that the method or property will ensure that the listed field and property members
    /// have non-null values when returning the specified return value condition.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
    internal sealed class MemberNotNullWhenAttribute : Attribute
    {
        /// <summary>
        /// Initializes the attribute with the specified return value condition and a field or property member.
        /// </summary>
        /// <param name="returnValue">
        /// The return value condition. If the method returns this value, the associated member will not be null.
        /// </param>
        /// <param name="member">The field or property member that is promised to be non-null.</param>
        public MemberNotNullWhenAttribute(bool returnValue, string member)
        {
            ReturnValue = returnValue;
            Members = [member];
        }

        /// <summary>
        /// Initializes the attribute with the specified return value condition and a list of field or property members.
        /// </summary>
        /// <param name="returnValue">
        /// The return value condition. If the method returns this value, the associated members will not be null.
        /// </param>
        /// <param name="members">The list of field or property members that are promised to be non-null.</param>
        public MemberNotNullWhenAttribute(bool returnValue, params string[] members)
        {
            ReturnValue = returnValue;
            Members = members;
        }

        /// <summary>
        /// Gets the return value condition.
        /// </summary>
        public bool ReturnValue { get; }

        /// <summary>
        /// Gets field or property member names.
        /// </summary>
        public string[] Members { get; }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    public sealed class MemberNotNullAttribute : Attribute
    {
        /// <summary>
        /// Gets the field or property member names.
        /// </summary>
        public string[] Members { get; }

        /// <summary>
        /// Initializes the attribute with a single member.
        /// </summary>
        public MemberNotNullAttribute(string member)
        {
            Members = [member];
        }

        /// <summary>
        /// Initializes the attribute with an array of members.
        /// </summary>
        public MemberNotNullAttribute(params string[] members)
        {
            Members = members;
        }
    }
}
