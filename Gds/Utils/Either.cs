using System;

namespace Gds.Utils
{
    /// <summary>
    /// Class that represent a Pair type that can only have one of its variables set.
    /// </summary>
    /// <typeparam name="TL">The possible type of the Left side</typeparam>
    /// <typeparam name="TR">The possible type of the Right side</typeparam>
    public class Either<TL, TR>
    {
        private readonly TL left;
        private readonly TR right;

        /// <summary>
        /// Returns whether the instance has its left side set.
        /// </summary>
        public bool IsLeft { get; }
        /// <summary>
        /// Returns whether the instance has its right side set.
        /// </summary>
        public bool IsRight => !IsLeft;

        /// <summary>
        /// Can be used to retrieve the Left side object.
        /// </summary>
        public TL Left
        {
            get
            {
                if (!IsLeft)
                {
                    throw new InvalidOperationException("Either does not have its left side set!");
                }
                return left;
            }
        }

        /// <summary>
        /// Can be used to retrieve the Right side object.
        /// </summary>
        public TR Right
        {
            get
            {
                if (IsLeft)
                {
                    throw new InvalidOperationException("Either does not have its right side set!");
                }
                return right;
            }
        }

        /// <summary>
        /// Constructs a new Either object with the left side specified
        /// </summary>
        /// <param name="left"></param>
        public Either(TL left)
        {
            this.left = left;
            this.IsLeft = true;
        }

        /// <summary>
        /// /// Constructs a new Either object with the right side specified
        /// </summary>
        /// <param name="right"></param>
        public Either(TR right)
        {
            this.right = right;
            this.IsLeft = false;
        }

        /// <summary>
        /// Returns the string representation based on which side is set.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return IsLeft ? left?.ToString() : right?.ToString();
        }

        /// <summary>
        /// implicitly creates an instance using a left value
        /// </summary>
        /// <param name="left">The object to be wrapped</param>
        public static implicit operator Either<TL, TR>(TL left) => new Either<TL, TR>(left);

        /// <summary>
        /// implicitly creates an instance using a right value
        /// </summary>
        /// <param name="right">The object to be wrapped</param>
        public static implicit operator Either<TL, TR>(TR right) => new Either<TL, TR>(right);
    }
}
