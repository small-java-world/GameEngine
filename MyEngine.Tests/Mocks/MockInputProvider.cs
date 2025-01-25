using System.Collections.Generic;
using MyEngine.Abstractions;

namespace MyEngine.Tests.Mocks
{
    public class MockInputProvider : IInputProvider
    {
        private int _frameCount = 0;
        private Dictionary<KeyCode, bool> _keyStates = new();

        public void Update()
        {
            _frameCount++;
        }

        public void SetKeyState(KeyCode code, bool isPressed)
        {
            _keyStates[code] = isPressed;
        }

        public bool IsKeyDown(KeyCode code)
        {
            return _keyStates.TryGetValue(code, out bool value) && value;
        }

        public bool IsKeyUp(KeyCode code)
        {
            return !IsKeyDown(code);
        }

        public int FrameCount => _frameCount;
    }
} 