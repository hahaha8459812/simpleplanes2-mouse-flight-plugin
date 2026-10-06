using System;
using UnityEngine;

namespace SimplePlanes2MouseFlight
{
    internal static class KeyUtil
    {
        // 按键名按 UnityEngine.KeyCode 的名字解析（例如 F8、F9、BackQuote）。
        // 解析失败退回默认键，不抛异常。
        public static KeyCode Parse(string text, KeyCode fallback)
        {
            if (text == null || text.Trim().Length == 0)
            {
                return fallback;
            }

            try
            {
                return (KeyCode)Enum.Parse(typeof(KeyCode), text.Trim(), true);
            }
            catch (Exception)
            {
                return fallback;
            }
        }
    }
}
