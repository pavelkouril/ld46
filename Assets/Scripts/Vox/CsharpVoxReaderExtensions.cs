using System;
using UnityEngine;

public static class UnityVoxReaderExtensions
{
    /// <summary>
    /// Read a <see cref="Color32"/> stored in a <see cref="uint"/> in the CsharpVoxReader palette
    /// </summary>
    /// <param name="value">Input value</param>
    public static Color32 ToColor(this uint value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        return new Color32(bytes[2], bytes[1], bytes[0], bytes[3]);
    }
}