using System;
using JetBrains.Annotations;

namespace BLPPCounter.Models.Base
{
    [UsedImplicitly]
    internal abstract class ModelId<T> : IModel, IEquatable<ModelId<T>>
        where T : IEquatable<T>
    {
        public abstract T Id { get; set; }

        public bool Equals(ModelId<T>? other)
        {
            if (other is null)
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return Id.Equals(other.Id);
        }

        public override bool Equals(object obj)
        {
            return obj is ModelId<T> other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }

    [UsedImplicitly]
    internal abstract class ModelId : ModelId<string> { }
}
