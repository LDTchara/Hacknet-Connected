using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Hacknet;
using HacknetHotReplace.Server.Handler.Impl;
using HacknetHotReplace.Util;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace HacknetHotReplace.Game
{
    [HarmonyPatch]
    public static class DrawAnalysis
    {
        private static AnalysisDrawCallReqWrapper _analysisDrawCallReq = null;


        private class AnalysisDrawCallReqWrapper
        {
            public AnalysisDrawCallReq Req { get; }

            public TaskCompletionSource<string> Tcs { get; }

            public bool IsTimeout => CommonUtils.GetCurrentTimespan() - Req.StartTimespan >= _timeout;

            public string Result { get; set; }

            public float LastMiniDistance { get; set; } = float.MaxValue;

            public bool RegisteredNextDrawCall { get; set; }

            private readonly int _timeout;

            private Point? middleClickPos;

            public AnalysisDrawCallReqWrapper(AnalysisDrawCallReq req, int timeout)
            {
                this.Req = req;
                this.Tcs = new TaskCompletionSource<string>();
                this._timeout = timeout;
            }

            public Point? CheckAndGetMiddleClickPos()
            {
                if (middleClickPos.HasValue)
                {
                    return middleClickPos.Value;
                }

                var mouseState = GuiData.lastMouse;
                if (mouseState.MiddleButton == ButtonState.Pressed)
                {
                    middleClickPos = GuiData.getMousePoint();
                }

                return middleClickPos;
            }
        }

        public static Task<string> AnalysisDrawCall(AnalysisDrawCallReq req, int timeout = 30000)
        {
            CommonUtils.AssertCurrentIsMainThread();
            if (_analysisDrawCallReq != null)
            {
                throw new InvalidOperationException("Please wait for the previous draw call analysis operation to complete");
            }

            _analysisDrawCallReq = new AnalysisDrawCallReqWrapper(req, timeout);
            return _analysisDrawCallReq.Tcs.Task;
        }

        private static string GetDrawCallStack()
        {
            var stackTrace = new StackTrace();
            var stackFrames = stackTrace.GetFrames();
            if (stackFrames == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();

            foreach (var stackFrame in stackFrames)
            {
                var line = $"[DRAW CALL] [IL:{stackFrame.GetILOffset()}] {stackFrame.GetMethod().DeclaringType?.FullName ?? "GLOBAL"}.{stackFrame.GetMethod().Name}";
                if (line.Contains(nameof(DrawAnalysis)))
                {
                    continue;
                }

                sb.AppendLine(line);
            }

            return sb.ToString();
        }

        private static void ExecuteAnalysisDrawCall(Predicate<AnalysisDrawCallReqWrapper> checkFunc)
        {
            if (_analysisDrawCallReq == null)
            {
                return;
            }

            if (_analysisDrawCallReq.IsTimeout)
            {
                _analysisDrawCallReq.Tcs.TrySetException(new TimeoutException("Analysis Draw Call Timeout"));
                _analysisDrawCallReq = null;
                return;
            }

            try
            {
                if (checkFunc(_analysisDrawCallReq))
                {
                    _analysisDrawCallReq.Result = GetDrawCallStack();
                    if (!_analysisDrawCallReq.RegisteredNextDrawCall)
                    {
                        CommonUtils.RegisterOnNextGameDraw(() =>
                        {
                            _analysisDrawCallReq.Tcs.SetResult(_analysisDrawCallReq.Result);
                            _analysisDrawCallReq = null;
                        });
                        _analysisDrawCallReq.RegisteredNextDrawCall = true;
                    }
                }
            }
            catch (Exception e)
            {
                _analysisDrawCallReq.Tcs.TrySetException(e);
                _analysisDrawCallReq = null;
                return;
            }
        }

        private static void ExecuteAnalysisDrawCallForDrawTexture(Rectangle destinationRectangle)
        {
            ExecuteAnalysisDrawCall(req =>
            {
                if (req.Req.Type != AnalysisDrawCallReq.AnalysisType_Graphics)
                {
                    return false;
                }

                var middleClickPos = req.CheckAndGetMiddleClickPos();
                if (!middleClickPos.HasValue)
                {
                    return false;
                }

                if (!destinationRectangle.Contains(middleClickPos.Value))
                {
                    return false;
                }

                if (GetDrawCallStack().Contains("Microsoft.Xna.Framework.Graphics.SpriteBatch::DrawString"))
                {
                    return false;
                }

                var distance = Vector2.Distance(new Vector2(destinationRectangle.Center.X, destinationRectangle.Center.Y), new Vector2(middleClickPos.Value.X, middleClickPos.Value.Y));
                if (distance < req.LastMiniDistance)
                {
                    req.LastMiniDistance = distance;
                    return true;
                }

                return false;
            });
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SpriteBatch), nameof(SpriteBatch.Draw),
            typeof(Texture2D), typeof(Vector2), typeof(Color))]
        public static bool FixSpriteBatchDraw1(SpriteBatch __instance, Texture2D texture, Vector2 position)
        {
            var destinationRectangle = new Rectangle((int)position.X, (int)position.Y, texture.Width, texture.Height);
            ExecuteAnalysisDrawCallForDrawTexture(destinationRectangle);

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SpriteBatch), nameof(SpriteBatch.Draw),
            typeof(Texture2D), typeof(Vector2), typeof(Rectangle?), typeof(Color))]
        public static bool FixSpriteBatchDraw2(SpriteBatch __instance, Texture2D texture, Vector2 position)
        {
            var destinationRectangle = new Rectangle((int)position.X, (int)position.Y, texture.Width, texture.Height);
            ExecuteAnalysisDrawCallForDrawTexture(destinationRectangle);

            return true;
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(SpriteBatch), nameof(SpriteBatch.Draw),
            typeof(Texture2D), typeof(Vector2), typeof(Rectangle?), typeof(Color), typeof(float), typeof(Vector2), typeof(float), typeof(SpriteEffects), typeof(float))]
        public static bool FixSpriteBatchDraw3(SpriteBatch __instance, Texture2D texture, Vector2 position)
        {
            var destinationRectangle = new Rectangle((int)position.X, (int)position.Y, texture.Width, texture.Height);
            ExecuteAnalysisDrawCallForDrawTexture(destinationRectangle);

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SpriteBatch), nameof(SpriteBatch.Draw),
            typeof(Texture2D), typeof(Vector2), typeof(Rectangle?), typeof(Color), typeof(float), typeof(Vector2), typeof(Vector2), typeof(SpriteEffects), typeof(float))]
        public static bool FixSpriteBatchDraw4(SpriteBatch __instance, Texture2D texture, Vector2 position)
        {
            var destinationRectangle = new Rectangle((int)position.X, (int)position.Y, texture.Width, texture.Height);
            ExecuteAnalysisDrawCallForDrawTexture(destinationRectangle);

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SpriteBatch), nameof(SpriteBatch.Draw),
            typeof(Texture2D), typeof(Rectangle), typeof(Color))]
        public static bool FixSpriteBatchDraw5(SpriteBatch __instance, Rectangle destinationRectangle)
        {
            ExecuteAnalysisDrawCallForDrawTexture(destinationRectangle);

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SpriteBatch), nameof(SpriteBatch.Draw),
            typeof(Texture2D), typeof(Rectangle), typeof(Rectangle?), typeof(Color))]
        public static bool FixSpriteBatchDraw6(SpriteBatch __instance, Rectangle destinationRectangle)
        {
            ExecuteAnalysisDrawCallForDrawTexture(destinationRectangle);

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SpriteBatch), nameof(SpriteBatch.Draw),
            typeof(Texture2D), typeof(Rectangle), typeof(Rectangle?), typeof(Color), typeof(float), typeof(Vector2), typeof(SpriteEffects), typeof(float))]
        public static bool FixSpriteBatchDraw7(SpriteBatch __instance, Rectangle destinationRectangle)
        {
            ExecuteAnalysisDrawCallForDrawTexture(destinationRectangle);

            return true;
        }



        [HarmonyPrefix]
        [HarmonyPatch(typeof(SpriteBatch), nameof(SpriteBatch.DrawString),
            typeof(SpriteFont), typeof(string), typeof(Vector2), typeof(Color), typeof(float),
            typeof(Vector2), typeof(Vector2), typeof(SpriteEffects), typeof(float))]
        public static bool FixDawString(SpriteBatch __instance, SpriteFont spriteFont, string text, Vector2 position)
        {
            ExecuteAnalysisDrawCall(req =>
            {
                var size = spriteFont.MeasureString(text);
                var destinationRectangle = new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y);
                if (req.Req.Type != AnalysisDrawCallReq.AnalysisType_Text)
                {
                    return false;
                }

                var middleClickPos = req.CheckAndGetMiddleClickPos();
                if (!middleClickPos.HasValue)
                {
                    return false;
                }

                if (!text.Contains(req.Req.Content))
                {
                    return false;
                }

                if (!destinationRectangle.Contains(middleClickPos.Value))
                {
                    return false;
                }

                var distance = Vector2.Distance(new Vector2(destinationRectangle.Center.X, destinationRectangle.Center.Y), new Vector2(middleClickPos.Value.X, middleClickPos.Value.Y));
                if (distance < req.LastMiniDistance)
                {
                    req.LastMiniDistance = distance;
                    return true;
                }

                return false;
            });

            return true;
        }
    }
}
