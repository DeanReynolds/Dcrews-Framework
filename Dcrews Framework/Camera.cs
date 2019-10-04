using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

namespace Dcrew.Framework
{
    public class Camera
    {
        public float X
        {
            get => _position.X;
            set
            {
                _position.X = value;
                UpdateRotationX();
                UpdatePosition();
            }
        }
        public float Y
        {
            get => _position.Y;
            set
            {
                _position.Y = value;
                UpdateRotationY();
                UpdatePosition();
            }
        }
        public Vector2 Position
        {
            get => _position;
            set
            {
                _position.X = value.X;
                _position.Y = value.Y;
                UpdateRotationX();
                UpdateRotationY();
                UpdatePosition();
            }
        }
        public float Angle
        {
            get => _angle;
            set
            {
                _rotM11 = (float)Math.Cos(-(_angle = value));
                _rotM12 = (float)Math.Sin(-_angle);
                UpdateScale();
                UpdateRotationX();
                UpdateRotationY();
                UpdatePosition();
            }
        }
        public (float X, float Y) Scale
        {
            get => _scale;
            set
            {
                _scale = value;
                UpdateScale();
                UpdateRotationX();
                UpdateRotationY();
                UpdatePosition();
            }
        }
        public (int Width, int Height) ViewportSize
        {
            get => _viewportSize;
            set
            {
                if (_viewportSize.Width != value.Width || _viewportSize.Height != value.Height)
                {
                    _viewportSize = value;
                    _viewportSizeOver2.X = _viewportSize.Width / 2f;
                    _viewportSizeOver2.Y = _viewportSize.Height / 2f;
                    var virtualScale = Math.Min(_viewportSize.Width / (float)_virtualScreenSize.Width, _viewportSize.Height / (float)_virtualScreenSize.Height);
                    _virtualScale = (virtualScale, virtualScale);
                    UpdateScale();
                    _projection.M11 = (float)(2d / _viewportSize.Width);
                    _projection.M22 = (float)(2d / -_viewportSize.Height);
                }
            }
        }
        public (int Width, int Height) VirtualScreenSize
        {
            get => _virtualScreenSize;
            set
            {
                _virtualScreenSize = value;
                var virtualScale = Math.Min(_viewportSize.Width / (float)_virtualScreenSize.Width, _viewportSize.Height / (float)_virtualScreenSize.Height);
                _virtualScale = (virtualScale, virtualScale);
                UpdateScale();
            }
        }

        public Matrix View => _view;
        public Vector2 MousePosition => _mousePosition;
        public Matrix Projection => _projection;

        Vector2 _position;
        float _angle;
        (int Width, int Height) _viewportSize;
        (float X, float Y) _viewportSizeOver2;
        (int Width, int Height) _virtualScreenSize;
        float _rotM11;
        float _rotM12;
        float _rotX1;
        float _rotY1;
        float _rotX2;
        float _rotY2;
        (float X, float Y) _scale;
        (float X, float Y) _virtualScale;
        (float X, float Y) _actualScale;
        double _n27;
        Matrix _view;
        float _invertM11;
        float _invertM12;
        float _invertM21;
        float _invertM22;
        float _invertM41;
        float _invertM42;
        Vector2 _mousePosition;
        Matrix _projection;

        public Camera(Vector2 position, float angle, (float X, float Y) scale, (int Width, int Height) virtualScreenSize)
        {
            _position.X = position.X;
            _position.Y = position.Y;
            _rotM11 = (float)Math.Cos(-(_angle = angle));
            _rotM12 = (float)Math.Sin(-_angle);
            _scale = scale;
            _viewportSize = (MGGame.Viewport.Width, MGGame.Viewport.Height);
            _viewportSizeOver2.X = _viewportSize.Width / 2f;
            _viewportSizeOver2.Y = _viewportSize.Height / 2f;
            _view = new Matrix
            {
                M33 = 1,
                M44 = 1
            };
            VirtualScreenSize = virtualScreenSize;
            _projection = new Matrix
            {
                M11 = (float)(2d / _viewportSize.Width),
                M22 = (float)(2d / -_viewportSize.Height),
                M33 = -1,
                M41 = -1,
                M42 = 1,
                M44 = 1
            };
        }

        public void UpdateMousePosition(MouseState? mouseState = null)
        {
            if (!mouseState.HasValue)
                mouseState = Mouse.GetState();
            var mouseX = mouseState.Value.Position.X;
            var mouseY = mouseState.Value.Position.Y;
            _mousePosition.X = (mouseX * _invertM11) + (mouseY * _invertM21) + _invertM41;
            _mousePosition.Y = (mouseX * _invertM12) + (mouseY * _invertM22) + _invertM42;
        }

        void UpdateRotationX()
        {
            var m41 = -_position.X * _actualScale.X;
            _rotX1 = m41 * _rotM11;
            _rotX2 = m41 * _rotM12;
        }

        void UpdateRotationY()
        {
            var m42 = -_position.Y * _actualScale.Y;
            _rotY1 = m42 * -_rotM12;
            _rotY2 = m42 * _rotM11;
        }

        void UpdatePosition()
        {
            _view.M41 = _rotX1 + _rotY1 + _viewportSizeOver2.X;
            _view.M42 = _rotX2 + _rotY2 + _viewportSizeOver2.Y;
            _invertM41 = (float)(-((double)_view.M21 * -_view.M42 - (double)_view.M22 * -_view.M41) * _n27);
            _invertM42 = (float)(((double)_view.M11 * -_view.M42 - (double)_view.M12 * -_view.M41) * _n27);
        }

        void UpdateScale()
        {
            _actualScale = (_scale.X * _virtualScale.X, _scale.Y * _virtualScale.Y);
            UpdateRotationX();
            UpdateRotationY();
            _view.M11 = _actualScale.X * _rotM11;
            _view.M21 = _actualScale.X * -_rotM12;
            _view.M12 = _actualScale.Y * _rotM12;
            _view.M22 = _actualScale.Y * _rotM11;
            _n27 = 1d / ((double)_view.M11 * _view.M22 + (double)_view.M12 * -_view.M21);
            UpdatePosition();
            _invertM11 = (float)(_view.M22 * _n27);
            _invertM21 = (float)(-_view.M21 * _n27);
            _invertM12 = (float)-(_view.M12 * _n27);
            _invertM22 = (float)(_view.M11 * _n27);
        }
    }
}