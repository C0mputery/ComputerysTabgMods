using System;

namespace ComputeryLib.Utilities;

public static class ArgumentUtility {
    public static bool TryGetArgument(string argumentName, out string? argumentValue) {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) {
            if (args[i] != argumentName) {
                continue;
            }
            argumentValue = args[i + 1];
            return true;
        }
        argumentValue = null;
        return false;
    }
}