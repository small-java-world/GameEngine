namespace MyEngine.Abstractions
{
    public interface IInputProvider
    {
        void Update();
        bool IsKeyDown(KeyCode code);
        bool IsKeyUp(KeyCode code);
    }

    public enum KeyCode
    {
        Space,
        Enter,
        Escape,
        Left,
        Right,
        Up,
        Down,
        A,
        B,
        C,
        D,
        E,
        F,
        G,
        H,
        I,
        J,
        K,
        L,
        M,
        N,
        O,
        P,
        Q,
        R,
        S,
        T,
        U,
        V,
        W,
        X,
        Y,
        Z
    }
} 