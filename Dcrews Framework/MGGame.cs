﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Dcrew.Framework
{
    public enum UpdateState { Cancelled, Finished }
    public enum DrawState { Cancelled, Finished }
    public enum WindowState { Windowed, Fullscreen, Borderless }

    public delegate void SizeChangedEvent(int oldWidth, int oldHeight);
    public delegate void RoomUpdate();
    public delegate UpdateState UpdateEvent();
    public delegate DrawState DrawEvent(SpriteBatchExtended spriteBatch);

    public class Room
    {
        public static Camera Camera
        {
            get => MGGame.Room._camera;
            set => MGGame.Room._camera = value;
        }

        public static void AddUpdate(UpdateEvent updateEvent) => MGGame.Room._updateEvents.Add(updateEvent);
        public static void RemoveUpdate(UpdateEvent updateEvent) => MGGame.Room._updateEvents.Remove(updateEvent);
        public static void RemoveUpdateAt(int i) => MGGame.Room._updateEvents.RemoveAt(i);
        public static void ClearUpdates() => MGGame.Room._updateEvents.Clear();
        public static void AddDraw(DrawEvent drawEvent) => MGGame.Room._drawEvents.Add(drawEvent);
        public static void RemoveDraw(DrawEvent drawEvent) => MGGame.Room._drawEvents.Remove(drawEvent);
        public static void RemoveDrawAt(int i) => MGGame.Room._drawEvents.RemoveAt(i);
        public static void ClearDraws() => MGGame.Room._drawEvents.Clear();

        Camera _camera;

        readonly IList<UpdateEvent> _updateEvents = new List<UpdateEvent>();
        readonly IList<DrawEvent> _drawEvents = new List<DrawEvent>();

        /// <summary>Called when this room becomes the active room</summary>
        public virtual void OnOpen()
        {
            _camera = _camera ?? new Camera(Vector2.Zero, 0, Vector2.One, new Vector2(MGGame.VirtualScreenSize.Width, MGGame.VirtualScreenSize.Height));
            MGGame.OnViewportSizeChanged += OnViewportSizeChanged;
            MGGame.OnVirtualScreenSizeChanged += OnVirtualScreenSizeChanged;
        }
        /// <summary>Called when the room changes or if the game is exited - while this room is open</summary>
        public virtual void OnClose()
        {
            MGGame.OnViewportSizeChanged -= OnViewportSizeChanged;
            MGGame.OnVirtualScreenSizeChanged -= OnVirtualScreenSizeChanged;
        }

        public virtual void Update()
        {
            for (var i = 0; i < _updateEvents.Count; i++)
                _updateEvents[i]();
        }
        public virtual void Draw(SpriteBatchExtended spriteBatch)
        {
            for (var i = 0; i < _drawEvents.Count; i++)
                _drawEvents[i](spriteBatch);
        }

        void OnViewportSizeChanged(int oldWidth, int oldHeight) => Camera.ViewportSize = new Vector2(MGGame.Viewport.Width, MGGame.Viewport.Height);
        void OnVirtualScreenSizeChanged(int oldWidth, int oldHeight) => Camera.VirtualScreenSize = new Vector2(MGGame.VirtualScreenSize.Width, MGGame.VirtualScreenSize.Height);
    }
    public static class Time
    {
        public static long DeltaTicks { get; internal set; }
        public static double DeltaTimeFull { get; internal set; }
        public static float DeltaTime { get; internal set; }
        public static double TotalTimeFull { get; internal set; }
        public static float TotalTime { get; internal set; }
    }
    public class SpriteBatchExtended : SpriteBatch
    {
        public SpriteBatchExtended(GraphicsDevice graphicsDevice) : base(graphicsDevice) { }
        public SpriteBatchExtended(GraphicsDevice graphicsDevice, int capacity) : base(graphicsDevice, capacity) { }

        public new void Begin(SpriteSortMode sortMode = SpriteSortMode.Deferred, BlendState blendState = null, SamplerState samplerState = null, DepthStencilState depthStencilState = null, RasterizerState rasterizerState = null, Effect effect = null, Matrix? transformMatrix = null)
        {
            if (!transformMatrix.HasValue)
                transformMatrix = MGGame._camera.View;
            base.Begin(sortMode, blendState, samplerState, depthStencilState, rasterizerState, effect, transformMatrix);
        }
    }
    public class MGGame : Game
    {
        public const long WINDOW_ACTIVE_UPDATE_TICKS = TimeSpan.TicksPerSecond / 60,
            WINDOW_INACTIVE_UPDATE_TICKS = TimeSpan.TicksPerSecond / 60;

        public static event SizeChangedEvent OnViewportSizeChanged,
            OnVirtualScreenSizeChanged;
        public static event RoomUpdate OnRoomOpen,
            OnRoomClosed;

        public static Room Room
        {
            get => _room;
            set
            {
                if (value != null && value != _room)
                {
                    _room.OnClose();
                    OnRoomClosed?.Invoke();
                    _room = value;
                    _room.OnOpen();
                    OnRoomOpen?.Invoke();
                }
            }
        }

        public static new GraphicsDevice GraphicsDevice { get; private set; }
        public static GraphicsDeviceManager Graphics { get; private set; }
        public static new ContentManager Content { get; private set; }
        public static SpriteBatchExtended SpriteBatch { get; private set; }
        public static new GameWindow Window { get; private set; }
        public static bool IsWindowActive { get; private set; }
        public static Viewport Viewport
        {
            get => GraphicsDevice.Viewport;
            private set => GraphicsDevice.Viewport = value;
        }
        public static (int Width, int Height, float HalfWidth, float HalfHeight) VirtualScreenSize { get; private set; }
        public static long TicksPerUpdate { get; private set; }

        internal static Camera _camera;

        static (int Width, int Height) _oldViewportSize,
            _oldPrefBackBufferSize;
        static Room _room;
        static Viewport _viewport;
        static bool _hasAsignedGfxDeviceReset;

        /// <summary>Sets the resolution the game is rendered at</summary>
        public static void SetVirtualRes(int width, int height)
        {
            if ((width != VirtualScreenSize.Width) || (height != VirtualScreenSize.Height))
            {
                int oldWidth = VirtualScreenSize.Width,
                    oldHeight = VirtualScreenSize.Height;
                VirtualScreenSize = (width, height, width / 2f, height / 2f);
                OnVirtualScreenSizeChanged?.Invoke(oldWidth, oldHeight);
                ForceVirtualResUpdate();
                if (!_hasAsignedGfxDeviceReset)
                {
                    Graphics.DeviceReset += Graphics_DeviceReset;
                    _hasAsignedGfxDeviceReset = true;
                }
            }
        }
        /// <summary>Sets the resolution of the game window</summary>
        /// <param name="width">0 will retain the current window width</param>
        /// <param name="height">0 will retain the current window height</param>
        /// <param name="windowState">null will retain the current window state</param>
        public static void SetRes(int width = 0, int height = 0, WindowState? windowState = null)
        {
            if (width > 0)
                Graphics.PreferredBackBufferWidth = width;
            if (height > 0)
                Graphics.PreferredBackBufferHeight = height;
            switch (windowState)
            {
                case WindowState.Windowed:
                    Graphics.IsFullScreen = false;
                    break;
                case WindowState.Fullscreen:
                    Graphics.HardwareModeSwitch = true;
                    Graphics.IsFullScreen = true;
                    break;
                case WindowState.Borderless:
                    Graphics.HardwareModeSwitch = false;
                    Graphics.IsFullScreen = true;
                    break;
            }
            Graphics.ApplyChanges();
        }

        static void ForceVirtualResUpdate()
        {
            var targetAspectRatio = VirtualScreenSize.Width / (float)VirtualScreenSize.Height;
            var width2 = Window.ClientBounds.Width;
            var height2 = (int)(width2 / targetAspectRatio + .5f);
            if (height2 > Window.ClientBounds.Height)
            {
                height2 = Window.ClientBounds.Height;
                width2 = (int)(height2 * targetAspectRatio + .5f);
            }
            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Viewport = _viewport = new Viewport()
            {
                X = (Window.ClientBounds.Width / 2) - (width2 / 2),
                Y = (Window.ClientBounds.Height / 2) - (height2 / 2),
                Width = width2,
                Height = height2
            };
            CheckViewportSizeChanged();
        }

        static void Graphics_DeviceReset(object sender, EventArgs e)
        {
            if (Graphics.PreferredBackBufferWidth != _oldPrefBackBufferSize.Width || Graphics.PreferredBackBufferHeight != _oldPrefBackBufferSize.Height)
            {
                ForceVirtualResUpdate();
                _oldPrefBackBufferSize = (Graphics.PreferredBackBufferWidth, Graphics.PreferredBackBufferHeight);
            }
        }

        static void CheckViewportSizeChanged()
        {
            if ((GraphicsDevice.Viewport.Width != _oldViewportSize.Width) || (GraphicsDevice.Viewport.Height != _oldViewportSize.Height))
            {
                OnViewportSizeChanged?.Invoke(_oldViewportSize.Width, _oldViewportSize.Height);
                //Console.WriteLine($"v: w {Viewport.Width} h {Viewport.Height}");
                _oldViewportSize = (Viewport.Width, Viewport.Height);
            }
        }

        public MGGame(Room room)
        {
            Graphics = new GraphicsDeviceManager(this)
            {
                GraphicsProfile = GraphicsProfile.HiDef,
                SynchronizeWithVerticalRetrace = false,
                PreferredBackBufferWidth = 1600,
                PreferredBackBufferHeight = 900
            };
            _oldPrefBackBufferSize = (Graphics.PreferredBackBufferWidth, Graphics.PreferredBackBufferHeight);
            Content = base.Content;
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            IsFixedTimeStep = true;
            Window = base.Window;
#pragma warning disable RECS0021
            if (IsActive)
                OnActivated(this, EventArgs.Empty);
            else
                OnDeactivated(this, EventArgs.Empty);
#pragma warning restore RECS0021
            _room = room;
        }

        protected override void Initialize()
        {
            SpriteBatchExtensions.Initialize(GraphicsDevice = base.GraphicsDevice);
            _oldViewportSize = (GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            _camera = _camera ?? new Camera(Vector2.Zero, 0, Vector2.One, Vector2.One);
            OnViewportSizeChanged += (int oldWidth, int oldHeight) => _camera.ViewportSize = new Vector2(Viewport.Width, Viewport.Height);
            OnVirtualScreenSizeChanged += (int oldWidth, int oldHeight) =>
            {
                _camera.VirtualScreenSize = new Vector2(VirtualScreenSize.Width, VirtualScreenSize.Height);
                _camera.Pos = new Vector2(VirtualScreenSize.HalfWidth, VirtualScreenSize.HalfHeight);
            };
            base.Initialize();
        }

        protected override void LoadContent()
        {
            SpriteBatch = new SpriteBatchExtended(GraphicsDevice);
            _room.OnOpen();
        }

        protected override void Update(GameTime gameTime)
        {
            CheckViewportSizeChanged();
            Time.DeltaTicks = gameTime.ElapsedGameTime.Ticks;
            Time.DeltaTime = (float)(Time.DeltaTimeFull = gameTime.ElapsedGameTime.TotalSeconds);
            Time.TotalTime = (float)(Time.TotalTimeFull = gameTime.TotalGameTime.TotalSeconds);
            if (IsWindowActive = IsActive)
                Input.Update();
            _camera.UpdateMousePosition(Input.MouseState);
            Timers.Update();
            Profiler.Update(gameTime);
            Profiler.Start("Update");
            _room.Update();
            Profiler.Stop("Update");
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            Profiler.Start("Draw");
            _room.Draw(SpriteBatch);
            Profiler.Stop("Draw");
            base.Draw(gameTime);
        }

        protected override void OnActivated(object sender, System.EventArgs args)
        {
            TargetElapsedTime = new TimeSpan(TicksPerUpdate = WINDOW_ACTIVE_UPDATE_TICKS);
            base.OnActivated(sender, args);
        }
        protected override void OnDeactivated(object sender, System.EventArgs args)
        {
            TargetElapsedTime = new TimeSpan(TicksPerUpdate = WINDOW_INACTIVE_UPDATE_TICKS);
            base.OnDeactivated(sender, args);
        }

        protected override void OnExiting(object sender, System.EventArgs args)
        {
            Room.OnClose();
            base.OnExiting(sender, args);
        }
    }
}