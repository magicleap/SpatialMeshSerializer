public static class InputHandler
{
    public static MagicLeapInput input { get; private set; }

    static InputHandler()
    {
        input = new();
        input.Enable();
    }
}