using System;

namespace Wizard.Content
{
    // CONTRACTS 0.2. 깨진 팩, 팩에 없는 절
    public sealed class ContentException : Exception
    {
        public ContentException(string message) : base(message)
        {
        }
    }
}
