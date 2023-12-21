namespace MixinLib
{
    class CallbackInfo
    {
        public readonly bool IsCancellable;
        public bool IsCanceled { get; private set; }

        public CallbackInfo(bool cancellable)
        {
            IsCancellable = cancellable;
        }

        public void Cancel()
        {
            if (!IsCancellable)
                throw new InvalidOperationException("cannot cancel non-cancel-able method");
            IsCanceled = true;
        }
    }

    class CallbackInfoReturn
    {
        public readonly bool IsCancellable;
        public object? ReturnValue { get; private set; }

        public CallbackInfoReturn(bool cancellable)
        {
            IsCancellable = cancellable;
        }

        public void Cancel(object returnVal)
        {
            if (!IsCancellable)
                throw new InvalidOperationException("cannot cancel non-cancel-able method");
            ReturnValue = returnVal;
        }
    }
}