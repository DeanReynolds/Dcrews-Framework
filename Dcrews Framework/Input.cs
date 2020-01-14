using Microsoft.Xna.Framework.Input;
using System;
using System.Linq;

namespace Dcrew.Framework
{
    public static class Input
    {
        public static event MouseEvent OnMousePressed;
        public static event MouseEvent OnMouseReleased;

        public static MouseState MouseState => _mouseState[_mouseUpdateIndex];
        public static float MouseX => MGGame._camera.MousePosition.X;
        public static float MouseY => MGGame._camera.MousePosition.Y;
        public static int MouseVerticalScroll => _mouseState[_mouseUpdateIndex].ScrollWheelValue;
        public static int MouseHorizontalScroll => _mouseState[_mouseUpdateIndex].HorizontalScrollWheelValue;
        public static bool MouseHeld(MouseButton button) => MouseHeld(0, (int)button);
        public static bool MouseHeldOnly(MouseButton button) => MouseHeldOnly(0, (int)button);
        public static bool MousePressed(MouseButton button) => MousePressed(0, (int)button);
        public static bool MouseReleased(MouseButton button) => MouseReleased(0, (int)button);
        public static KeyboardState KeyboardState => _keyboardState[_keyboardUpdateIndex];
        public static bool KeyHeld(Keys key) => _keyboardState[_keyboardUpdateIndex].IsKeyDown(key);
        public static bool KeyHeldOnly(Keys key) => _keyboardState[_keyboardUpdateIndex].IsKeyDown(key) && _oldKeyboardState[_keyboardUpdateIndex].IsKeyDown(key);
        public static bool KeyPressed(Keys key) => _keyboardState[_keyboardUpdateIndex].IsKeyDown(key) && _oldKeyboardState[_keyboardUpdateIndex].IsKeyUp(key);
        public static bool KeyReleased(Keys key) => _keyboardState[_keyboardUpdateIndex].IsKeyUp(key) && _oldKeyboardState[_keyboardUpdateIndex].IsKeyDown(key);

        static int _mouseUpdateIndex;
        static int _keyboardUpdateIndex;

        static readonly int[] _mouseButtons = Enum.GetValues(typeof(MouseButton)).Cast<int>().ToArray();
        static readonly MouseState[] _mouseState = new MouseState[1];
        static readonly MouseState[] _oldMouseState = new MouseState[_mouseState.Length];
        static readonly ButtonState[][] _mouseButtonStates = new ButtonState[_mouseState.Length][];
        static readonly ButtonState[][] _oldMouseButtonStates = new ButtonState[_mouseState.Length][];
        static readonly KeyboardState[] _keyboardState = new KeyboardState[1];
        static readonly KeyboardState[] _oldKeyboardState = new KeyboardState[_keyboardState.Length];

        static Input()
        {
            for (int i = 0; i < _mouseState.Length; i++)
            {
                _mouseButtonStates[i] = new ButtonState[_mouseButtons.Length];
                _oldMouseButtonStates[i] = new ButtonState[_mouseButtons.Length];
            }
        }

        public delegate void MouseEvent(MouseButton button);
        public enum MouseButton { Left = 0, Right = 1, Middle = 2, XButton1 = 3, XButton2 = 4 }

        internal static void Update()
        {
            _mouseUpdateIndex = 0;
            UpdateMouseStates(0);
            if (OnMousePressed != null)
            {
                if (OnMouseReleased != null)
                    for (int i = 0; i < _mouseButtons.Length; i++)
                    {
                        if (MousePressed(0, _mouseButtons[i]))
                            OnMousePressed((MouseButton)_mouseButtons[i]);
                        if (MouseReleased(0, _mouseButtons[i]))
                            OnMouseReleased((MouseButton)_mouseButtons[i]);
                    }
                else
                    for (int i = 0; i < _mouseButtons.Length; i++)
                        if (MousePressed(0, _mouseButtons[i]))
                            OnMousePressed((MouseButton)_mouseButtons[i]);
            }
            else if (OnMouseReleased != null)
                for (int i = 0; i < _mouseButtons.Length; i++)
                    if (MouseReleased(0, _mouseButtons[i]))
                        OnMouseReleased((MouseButton)_mouseButtons[i]);
            _keyboardUpdateIndex = 0;
            UpdateKeyboardStates(0);
        }

        static bool MouseHeld(int updateType, int button) => MouseHeld(_mouseButtonStates[updateType][button]);
        static bool MouseHeldOnly(int updateType, int button) => MouseHeldOnly(_mouseButtonStates[updateType][button], _oldMouseButtonStates[updateType][button]);
        static bool MousePressed(int updateType, int button) => MousePressed(_mouseButtonStates[updateType][button], _oldMouseButtonStates[updateType][button]);
        static bool MouseReleased(int updateType, int button) => MouseReleased(_mouseButtonStates[updateType][button], _oldMouseButtonStates[updateType][button]);
        static bool MouseHeld(ButtonState buttonState) => (buttonState == ButtonState.Pressed);
        static bool MouseHeldOnly(ButtonState buttonState, ButtonState oldButtonState) => ((buttonState == ButtonState.Pressed) && (oldButtonState == ButtonState.Pressed));
        static bool MousePressed(ButtonState buttonState, ButtonState oldButtonState) => ((buttonState == ButtonState.Pressed) && (oldButtonState == ButtonState.Released));
        static bool MouseReleased(ButtonState buttonState, ButtonState oldButtonState) => ((buttonState == ButtonState.Released) && (oldButtonState == ButtonState.Pressed));

        static void UpdateMouseStates(int updateIndex)
        {
            var mouseState = _oldMouseState[updateIndex] = _mouseState[updateIndex];
            _oldMouseButtonStates[updateIndex][0] = mouseState.LeftButton;
            _oldMouseButtonStates[updateIndex][1] = mouseState.RightButton;
            _oldMouseButtonStates[updateIndex][2] = mouseState.MiddleButton;
            _oldMouseButtonStates[updateIndex][3] = mouseState.XButton1;
            _oldMouseButtonStates[updateIndex][4] = mouseState.XButton2;
            mouseState = _mouseState[updateIndex] = Mouse.GetState();
            _mouseButtonStates[updateIndex][0] = mouseState.LeftButton;
            _mouseButtonStates[updateIndex][1] = mouseState.RightButton;
            _mouseButtonStates[updateIndex][2] = mouseState.MiddleButton;
            _mouseButtonStates[updateIndex][3] = mouseState.XButton1;
            _mouseButtonStates[updateIndex][4] = mouseState.XButton2;
        }

        static void UpdateKeyboardStates(int updateIndex)
        {
            _oldKeyboardState[updateIndex] = _keyboardState[updateIndex];
            _keyboardState[updateIndex] = Keyboard.GetState();
        }
    }
}