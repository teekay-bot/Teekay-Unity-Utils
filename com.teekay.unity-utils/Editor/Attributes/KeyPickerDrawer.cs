using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TeekayUtils.EditorTools
{
    /// <summary>
    /// Property drawer for <c>[KeyPicker] Key someKey</c> fields. Replaces Unity's default
    /// EnumPopup (a 100+ entry alphabetical list) with a click-to-listen capture button:
    ///
    ///   • Click the button → it highlights and shows "Press any key…"
    ///   • Press a key on the keyboard → the field updates and capture ends
    ///   • Press Escape → cancel without changing
    ///
    /// One PropertyDrawer instance is reused across multiple fields; per-field capture
    /// state is tracked by serialized property path in <see cref="s_capturing"/>.
    /// </summary>
    [CustomPropertyDrawer(typeof(KeyPickerAttribute))]
    public sealed class KeyPickerDrawer : PropertyDrawer
    {
        static readonly HashSet<string> s_capturing = new();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Enum)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height);
            var btnRect   = new Rect(position.x + EditorGUIUtility.labelWidth, position.y,
                                     position.width - EditorGUIUtility.labelWidth, position.height);

            EditorGUI.LabelField(labelRect, label);

            string path = property.propertyPath;
            bool isCapturing = s_capturing.Contains(path);

            var current = (Key)property.intValue;
            string display = isCapturing
                ? "Press any key…  (Esc to cancel)"
                : current == Key.None ? "(None)" : current.ToString();

            var prevBg = GUI.backgroundColor;
            if (isCapturing) GUI.backgroundColor = new Color(0.3f, 0.6f, 1f);
            if (GUI.Button(btnRect, display))
            {
                if (isCapturing) s_capturing.Remove(path);
                else s_capturing.Add(path);
                Event.current.Use();
                EditorWindow.focusedWindow?.Repaint();
            }
            GUI.backgroundColor = prevBg;

            if (!isCapturing) return;

            // Force repaint so the highlighted "Press any key…" state animates / stays current.
            EditorWindow.focusedWindow?.Repaint();

            var e = Event.current;
            if (e.type != EventType.KeyDown) return;

            if (e.keyCode == KeyCode.Escape)
            {
                s_capturing.Remove(path);
                e.Use();
                return;
            }

            var mapped = KeyCodeToKey(e.keyCode);
            if (mapped == Key.None) return;

            property.intValue = (int)mapped;
            property.serializedObject.ApplyModifiedProperties();
            s_capturing.Remove(path);
            e.Use();
        }

        static Key KeyCodeToKey(KeyCode k) => k switch
        {
            KeyCode.Backspace => Key.Backspace, KeyCode.Delete => Key.Delete,
            KeyCode.Tab => Key.Tab, KeyCode.Return => Key.Enter,
            KeyCode.Pause => Key.Pause, KeyCode.Space => Key.Space,
            KeyCode.UpArrow => Key.UpArrow, KeyCode.DownArrow => Key.DownArrow,
            KeyCode.LeftArrow => Key.LeftArrow, KeyCode.RightArrow => Key.RightArrow,
            KeyCode.Insert => Key.Insert, KeyCode.Home => Key.Home, KeyCode.End => Key.End,
            KeyCode.PageUp => Key.PageUp, KeyCode.PageDown => Key.PageDown,
            KeyCode.F1 => Key.F1, KeyCode.F2 => Key.F2, KeyCode.F3 => Key.F3, KeyCode.F4 => Key.F4,
            KeyCode.F5 => Key.F5, KeyCode.F6 => Key.F6, KeyCode.F7 => Key.F7, KeyCode.F8 => Key.F8,
            KeyCode.F9 => Key.F9, KeyCode.F10 => Key.F10, KeyCode.F11 => Key.F11, KeyCode.F12 => Key.F12,
            KeyCode.Alpha0 => Key.Digit0, KeyCode.Alpha1 => Key.Digit1, KeyCode.Alpha2 => Key.Digit2,
            KeyCode.Alpha3 => Key.Digit3, KeyCode.Alpha4 => Key.Digit4, KeyCode.Alpha5 => Key.Digit5,
            KeyCode.Alpha6 => Key.Digit6, KeyCode.Alpha7 => Key.Digit7, KeyCode.Alpha8 => Key.Digit8,
            KeyCode.Alpha9 => Key.Digit9,
            KeyCode.Keypad0 => Key.Numpad0, KeyCode.Keypad1 => Key.Numpad1, KeyCode.Keypad2 => Key.Numpad2,
            KeyCode.Keypad3 => Key.Numpad3, KeyCode.Keypad4 => Key.Numpad4, KeyCode.Keypad5 => Key.Numpad5,
            KeyCode.Keypad6 => Key.Numpad6, KeyCode.Keypad7 => Key.Numpad7, KeyCode.Keypad8 => Key.Numpad8,
            KeyCode.Keypad9 => Key.Numpad9,
            KeyCode.KeypadPeriod => Key.NumpadPeriod, KeyCode.KeypadDivide => Key.NumpadDivide,
            KeyCode.KeypadMultiply => Key.NumpadMultiply, KeyCode.KeypadMinus => Key.NumpadMinus,
            KeyCode.KeypadPlus => Key.NumpadPlus, KeyCode.KeypadEnter => Key.NumpadEnter,
            KeyCode.KeypadEquals => Key.NumpadEquals,
            KeyCode.Minus => Key.Minus, KeyCode.Equals => Key.Equals,
            KeyCode.LeftBracket => Key.LeftBracket, KeyCode.RightBracket => Key.RightBracket,
            KeyCode.Backslash => Key.Backslash, KeyCode.Semicolon => Key.Semicolon,
            KeyCode.Quote => Key.Quote, KeyCode.Comma => Key.Comma,
            KeyCode.Period => Key.Period, KeyCode.Slash => Key.Slash,
            KeyCode.BackQuote => Key.Backquote,
            KeyCode.A => Key.A, KeyCode.B => Key.B, KeyCode.C => Key.C, KeyCode.D => Key.D,
            KeyCode.E => Key.E, KeyCode.F => Key.F, KeyCode.G => Key.G, KeyCode.H => Key.H,
            KeyCode.I => Key.I, KeyCode.J => Key.J, KeyCode.K => Key.K, KeyCode.L => Key.L,
            KeyCode.M => Key.M, KeyCode.N => Key.N, KeyCode.O => Key.O, KeyCode.P => Key.P,
            KeyCode.Q => Key.Q, KeyCode.R => Key.R, KeyCode.S => Key.S, KeyCode.T => Key.T,
            KeyCode.U => Key.U, KeyCode.V => Key.V, KeyCode.W => Key.W, KeyCode.X => Key.X,
            KeyCode.Y => Key.Y, KeyCode.Z => Key.Z,
            KeyCode.LeftShift => Key.LeftShift, KeyCode.RightShift => Key.RightShift,
            KeyCode.LeftControl => Key.LeftCtrl, KeyCode.RightControl => Key.RightCtrl,
            KeyCode.LeftAlt => Key.LeftAlt, KeyCode.RightAlt => Key.RightAlt,
            KeyCode.LeftCommand => Key.LeftMeta, KeyCode.RightCommand => Key.RightMeta,
            KeyCode.CapsLock => Key.CapsLock, KeyCode.ScrollLock => Key.ScrollLock,
            KeyCode.Numlock => Key.NumLock,
            KeyCode.Print => Key.PrintScreen,
            _ => Key.None
        };
    }
}
