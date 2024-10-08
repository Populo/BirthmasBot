﻿namespace Birthmas.Bot.Modules;

// https://stackoverflow.com/questions/8200661/how-to-align-string-in-fixed-length-string
public static class StringExtensions
{
    public static string CenterString(this string stringToCenter, int totalLength)
    {
        return stringToCenter.PadLeft(((totalLength - stringToCenter.Length) / 2) + stringToCenter.Length).PadRight(totalLength);
    }

    public static string CenterString(this string stringToCenter, int totalLength, char paddingCharacter)
    {
        return stringToCenter.PadLeft(((totalLength - stringToCenter.Length) / 2) + stringToCenter.Length, paddingCharacter).PadRight(totalLength, paddingCharacter);
    }
}