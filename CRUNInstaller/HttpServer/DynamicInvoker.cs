using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;

namespace CRUNInstaller.HttpServer
{
    internal class DynamicInvoker
    {
        //Gemini AI generated class :c

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr hModule);

        private static readonly Dictionary<string, (Type Type, Func<string, CharSet, List<IntPtr>, object> Parser)> TypeParsers = new Dictionary<string, (Type, Func<string, CharSet, List<IntPtr>, object>)>(StringComparer.OrdinalIgnoreCase)
        {
            { "intptr", (typeof(IntPtr), (literal, cs, ptrs) => new IntPtr(long.Parse(literal))) },
            { "string", (typeof(IntPtr), (literal, cs, ptrs) => { // String returns IntPtr for the call
                    IntPtr ptr;
                    switch (cs)
                    {
                        case CharSet.Unicode:
                            ptr = Marshal.StringToHGlobalUni(literal);
                            break;
                        case CharSet.Ansi:
                            ptr = Marshal.StringToHGlobalAnsi(literal);
                            break;
                        case CharSet.Auto:
                        default: // Default to Auto if unspecified or invalid
                            ptr = Marshal.StringToHGlobalAuto(literal);
                            break;
                    }
                    if (ptr != IntPtr.Zero) { // Track only if allocation succeeded
                        ptrs.Add(ptr); // IMPORTANT: Track pointer for freeing later
                    }
                    return ptr;
                })
            },
            { "ulong",  (typeof(ulong),  (literal, cs, ptrs) => ulong.Parse(literal)) },
            { "long",   (typeof(long),   (literal, cs, ptrs) => long.Parse(literal)) },
            { "uint",   (typeof(uint),   (literal, cs, ptrs) => uint.Parse(literal)) },
            { "int",    (typeof(int),    (literal, cs, ptrs) => int.Parse(literal)) },
            { "ushort", (typeof(ushort), (literal, cs, ptrs) => ushort.Parse(literal)) },
            { "short",  (typeof(short),  (literal, cs, ptrs) => short.Parse(literal)) },
            { "byte",   (typeof(byte),   (literal, cs, ptrs) => byte.Parse(literal)) }, // Added byte/sbyte
            { "sbyte",  (typeof(sbyte),  (literal, cs, ptrs) => sbyte.Parse(literal)) },
            { "bool",   (typeof(bool),   (literal, cs, ptrs) => bool.Parse(literal)) },
            { "float",  (typeof(float),  (literal, cs, ptrs) => float.Parse(literal)) }, // Added float/double
            { "double", (typeof(double), (literal, cs, ptrs) => double.Parse(literal)) },
            // Add other primitive/blittable types as needed
        };

        /// <summary>
        /// Dynamically invokes a native function from a DLL.
        /// </summary>
        /// <param name="dllName">The name or path of the DLL.</param>
        /// <param name="methodName">The name of the exported function.</param>
        /// <param name="paramList">A comma-separated string of parameters in "type=value" format (e.g., "int=10, string=hello, bool=true").</param>
        /// <param name="callConv">The calling convention of the native function.</param>
        /// <param name="returnType">The expected return type of the function.</param>
        /// <param name="charSet">The character set to use for marshalling strings (Ansi, Unicode, Auto).</param>
        /// <returns>The return value from the native function, boxed if necessary.</returns>
        /// <exception cref="ArgumentException">Thrown for invalid parameter format or unsupported types.</exception>
        /// <exception cref="DllNotFoundException">Thrown if the DLL cannot be loaded.</exception>
        /// <exception cref="EntryPointNotFoundException">Thrown if the function cannot be found in the DLL.</exception>
        /// <exception cref="NotSupportedException">Thrown for unsupported parameter types.</exception>
        public static string InvokeSingle(
            string dllName,
            string methodName,
            string paramList,
            CallingConvention callConv,
            Type returnType,
            CharSet charSet = CharSet.Auto) // Default to Auto CharSet
        {
            var tokens = paramList? // Handle potentially null paramList
                .Split([','], StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .ToArray() ?? []; // Default to empty array if null or empty

            var types = new Type[tokens.Length];
            var values = new object[tokens.Length];
            var stringPointersToFree = new List<IntPtr>(); // Track allocated string pointers

            try
            {
                // 1. Parse Parameters
                for (int i = 0; i < tokens.Length; i++)
                {
                    var parts = tokens[i].Split(['='], 2);
                    if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
                        throw new ArgumentException($"Invalid parameter format: '{tokens[i]}'. Expected 'type=value'.");

                    string typeName = parts[0].Trim();
                    string literal = parts[1].Trim();

                    if (TypeParsers.TryGetValue(typeName, out var parserInfo))
                    {
                        try
                        {
                            types[i] = parserInfo.Type;
                            // Pass charSet and the list to track pointers to the parser
                            values[i] = parserInfo.Parser(literal, charSet, stringPointersToFree);
                        }
                        catch (FormatException ex)
                        {
                            throw new ArgumentException($"Failed to parse value '{literal}' for type '{typeName}'.", ex);
                        }
                        catch (OverflowException ex)
                        {
                            throw new ArgumentException($"Value '{literal}' is out of range for type '{typeName}'.", ex);
                        }
                    }
                    else
                    {
                        throw new NotSupportedException($"Parameter type '{typeName}' is not supported.");
                    }
                }

                // 2. Load Library and Get Function Pointer
                IntPtr hLib = LoadLibrary(dllName);
                if (hLib == IntPtr.Zero)
                    throw new DllNotFoundException($"Failed to load DLL '{dllName}'. Error code: {Marshal.GetLastWin32Error()}");

                try // Inner try ensures FreeLibrary runs even if GetProcAddress fails
                {
                    IntPtr pfn = GetProcAddress(hLib, methodName);
                    if (pfn == IntPtr.Zero)
                        throw new EntryPointNotFoundException($"Function '{methodName}' not found in '{dllName}'. Error code: {Marshal.GetLastWin32Error()}");

                    // 3. Build the DynamicMethod Trampoline
                    var dm = new DynamicMethod(
                        $"__dyn_{Guid.NewGuid():N}", // Unique name
                        typeof(object),             // Return type (always object, boxed)
                        [typeof(object[])],         // Single argument: object array
                        typeof(DynamicInvoker).Module, // Associated module
                        skipVisibility: true);       // Allow access to private types if needed

                    var il = dm.GetILGenerator();

                    // Unpack arguments from object[] onto the stack
                    for (int i = 0; i < types.Length; i++)
                    {
                        il.Emit(OpCodes.Ldarg_0);      // Load object[] argument
                        il.Emit(OpCodes.Ldc_I4, i);    // Load index i
                        il.Emit(OpCodes.Ldelem_Ref);   // Get element args[i] (which is an object)
                        EmitUnboxOrCast(il, types[i]); // Unbox/cast to the required native type
                    }

                    // Push the function pointer
                    if (IntPtr.Size == 8)
                        il.Emit(OpCodes.Ldc_I8, pfn.ToInt64());
                    else
                        il.Emit(OpCodes.Ldc_I4, pfn.ToInt32());
                    il.Emit(OpCodes.Conv_I); // Convert to native int (IntPtr)

                    // Call the native function
                    il.EmitCalli(OpCodes.Calli, callConv, returnType, types);

                    // Handle return value
                    if (returnType == typeof(void))
                    {
                        il.Emit(OpCodes.Ldnull); // Return null for void
                    }
                    else if (returnType.IsValueType)
                    {
                        il.Emit(OpCodes.Box, returnType); // Box value types
                    }
                    // Reference types are already objects, no boxing needed

                    il.Emit(OpCodes.Ret); // Return the result (or null)

                    // Create the delegate
                    var invoker = (Func<object[], object>)dm.CreateDelegate(typeof(Func<object[], object>));

                    // 4. Invoke the Trampoline
                    var result = invoker(values);

                    return FormatReturnValue(result, returnType, charSet);
                }
                finally
                {
                    if (hLib != IntPtr.Zero)
                    {
                        FreeLibrary(hLib); // Ensure library is freed
                    }
                }
            }
            finally // Ensure allocated string memory is always freed
            {
                foreach (IntPtr ptr in stringPointersToFree)
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
        }

        private static string FormatReturnValue(object? rawReturnValue, Type expectedReturnType, CharSet charSet)
        {
            // Case 1: Expected void
            if (expectedReturnType == typeof(void))
            {
                // The dynamic method should return null in this case.
                return "void";
            }

            // Case 2: Raw return value is null
            if (rawReturnValue == null)
            {
                // Could be a null pointer/handle returned, or an issue before boxing.
                return "null";
            }

            // Case 3: Expected a string, and got an IntPtr (common for C functions returning char* or wchar_t*)
            // We interpret the IntPtr as a pointer to a string based on the expected type.
            if ((expectedReturnType == typeof(string) || expectedReturnType.Name.StartsWith("LP")) // Simple heuristic for LPStr etc.
                 && rawReturnValue is IntPtr ptr && ptr != IntPtr.Zero)
            {
                try
                {
                    string? resultString = null;
                    switch (charSet)
                    {
                        case CharSet.Ansi:
                            resultString = Marshal.PtrToStringAnsi(ptr);
                            break;
                        case CharSet.Unicode:
                        case CharSet.Auto: // Assume Auto means Unicode for return values on modern Windows
                        default:
                            resultString = Marshal.PtrToStringUni(ptr);
                            break;
                    }
                    // Note: We generally DO NOT free the memory pointed to by a return value unless the API docs explicitly say to.
                    return resultString ?? "(empty string)"; // Or "(null string)" if PtrToString returns null
                }
                catch (AccessViolationException) { return $"(Access violation reading string from 0x{ptr.ToInt64():X})"; }
                catch (Exception ex) { return $"(Error reading string from 0x{ptr.ToInt64():X}: {ex.GetType().Name})"; }
            }

            // Case 4: Return value is an IntPtr or UIntPtr (treat as handle or address)
            // Format as hexadecimal, which is conventional for pointers/handles.
            if (rawReturnValue is IntPtr ptrValue)
            {
                return $"0x{ptrValue.ToInt64():X}"; // e.g., 0x0012FBD0
            }
            if (rawReturnValue is UIntPtr uptrValue)
            {
                return $"0x{uptrValue.ToUInt64():X}";
            }

            // Case 5: Return value is already a C# string (less likely with EmitCalli, but possible if returnType was string)
            if (rawReturnValue is string strValue)
            {
                return strValue; // Return the string directly
            }

            // Case 6: Other primitive types, enums, structs (use default ToString)
            // This covers bool, int, uint, double, enums, structs, etc.
            // The object is likely boxed, but ToString() usually works well.
            try
            {
                return rawReturnValue.ToString() ?? "null";
            }
            catch (Exception ex)
            {
                // Handle potential exceptions from ToString() on complex/problematic types
                return $"(Error converting {rawReturnValue.GetType().Name} to string: {ex.Message})";
            }
        }

        public static readonly Dictionary<string, Type> TypeMap = InitializeTypeMap();

        // Initialize the dictionary with primitive and common WinAPI types/aliases
        private static Dictionary<string, Type> InitializeTypeMap()
        {
            // Using OrdinalIgnoreCase for case-insensitive matching (e.g., "int" vs "INT")
            var map = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

            // --- .NET Primitive Types ---
           /* map.Add("bool", typeof(bool));
            map.Add("byte", typeof(byte));
            map.Add("sbyte", typeof(sbyte));
            map.Add("char", typeof(char)); // Represents a single character value (System.Char), often maps to WCHAR
            map.Add("short", typeof(short));
            map.Add("ushort", typeof(ushort));
            map.Add("int", typeof(int));
            map.Add("uint", typeof(uint));
            map.Add("long", typeof(long));     // Be mindful of 32/64-bit differences if mapping WinAPI LONG/ULONG directly
            map.Add("ulong", typeof(ulong));
            map.Add("float", typeof(float));
            map.Add("double", typeof(double));
            map.Add("decimal", typeof(decimal)); // Less common in WinAPI signatures but included for completeness*/
            map.Add("void", typeof(void));     // For specifying return type

            // --- Pointer/Handle Types ---
            map.Add("IntPtr", typeof(IntPtr)); // Platform-specific pointer size
            map.Add("UIntPtr", typeof(UIntPtr));// Platform-specific unsigned pointer size

            // --- String Pointer Types (all map to IntPtr for the P/Invoke signature) ---
            // The specific marshalling (Ansi/Unicode) is determined later during *value parsing*,
            // based on the original type name string ("LPStr", "LPWStr", etc.) and the CharSet.
            map.Add("string", typeof(IntPtr)); // Map C# 'string' keyword when used as a P/Invoke param type to IntPtr
            map.Add("LPStr", typeof(IntPtr));    // -> char*
            map.Add("LPCStr", typeof(IntPtr));   // -> const char*
            map.Add("LPWStr", typeof(IntPtr)); // -> wchar_t*
            map.Add("LPCWStr", typeof(IntPtr));// -> const wchar_t*
            map.Add("LPTStr", typeof(IntPtr));   // -> TCHAR* (actual marshalling depends on CharSet)

            // --- Common WinAPI Type Aliases ---
            // Map aliases to their typical underlying .NET equivalent type for the P/Invoke signature.
            // Note: The exact size/signedness can sometimes vary; these are the common mappings.
            //map.Add("BOOL", typeof(int));      // WinAPI BOOL is typically a 32-bit integer (0 = FALSE, non-zero = TRUE)
            map.Add("BOOLEAN", typeof(byte));  // WinAPI BOOLEAN is typically a byte (0 = FALSE, non-zero = TRUE)
            map.Add("DWORD", typeof(uint));    // 32-bit unsigned integer
            map.Add("ULONG", typeof(uint));    // Often 32-bit unsigned on Windows (ULONG_PTR matches platform pointer size)
            map.Add("LONG", typeof(int));      // Often 32-bit signed on Windows (LONG_PTR matches platform pointer size)
            map.Add("UINT", typeof(uint));     // Unsigned integer
            map.Add("INT", typeof(int));       // Signed integer
            map.Add("WORD", typeof(ushort));   // 16-bit unsigned integer
            map.Add("SHORT", typeof(short));   // 16-bit signed integer
            map.Add("BYTE", typeof(byte));     // 8-bit unsigned integer
            map.Add("WCHAR", typeof(char));    // Represents a wide character (maps to System.Char which is UTF-16)

            // Common Handles (all map to IntPtr)
            map.Add("HANDLE", typeof(IntPtr));
            map.Add("HWND", typeof(IntPtr));
            map.Add("HMODULE", typeof(IntPtr));
            map.Add("HINSTANCE", typeof(IntPtr));
            map.Add("HICON", typeof(IntPtr));
            map.Add("HCURSOR", typeof(IntPtr));
            map.Add("HBRUSH", typeof(IntPtr));
            map.Add("HBITMAP", typeof(IntPtr));
            map.Add("HDC", typeof(IntPtr));
            map.Add("HGDIOBJ", typeof(IntPtr));
            map.Add("HLOCAL", typeof(IntPtr));
            map.Add("HGLOBAL", typeof(IntPtr));
            // Add other specific handles as needed...

            // TODO: Consider adding mappings for common structures if they are defined
            // map.Add("POINT", typeof(YourNamespace.POINT));
            // map.Add("RECT", typeof(YourNamespace.RECT));

            return map;
        }


        /// <summary>
        /// Helper to emit the correct IL instruction for unboxing or casting.
        /// </summary>
        private static void EmitUnboxOrCast(ILGenerator il, Type type)
        {
            if (type.IsValueType)
            {
                il.Emit(OpCodes.Unbox_Any, type);
            }
            else
            {
                // Includes IntPtr and other reference types (though we mainly use IntPtr for strings here)
                il.Emit(OpCodes.Castclass, type);
            }
        }
    }
}
