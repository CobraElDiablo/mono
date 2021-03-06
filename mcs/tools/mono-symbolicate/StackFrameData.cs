using System;
using System.Text.RegularExpressions;
using System.Globalization;

namespace Mono
{
	class StackFrameData
	{
		static Regex regex = new Regex (@"\w*at (?<Method>.+) *(\[0x(?<IL>.+)\]|<0x.+ \+ 0x(?<NativeOffset>.+)>( (?<MethodIndex>\d+)|)) in <filename unknown>:0");

		public readonly string TypeFullName;
		public readonly string MethodSignature;
		public readonly int Offset;
		public readonly bool IsILOffset;
		public readonly uint MethodIndex;
		public readonly string Line;

		public readonly bool IsValid;

		public string File { get; private set; }
		public int LineNumber { get; private set; }

		private StackFrameData (string line, string typeFullName, string methodSig, int offset, bool isILOffset, uint methodIndex)
		{
			LineNumber = -1;

			Line = line;
			TypeFullName = typeFullName;
			MethodSignature = methodSig;
			Offset = offset;
			IsILOffset = isILOffset;
			MethodIndex = methodIndex;

			IsValid = true;
		}

		private StackFrameData (string line)
		{
			LineNumber = -1;

			Line = line;
		}

		public static bool TryParse (string line, out StackFrameData stackFrame)
		{
			stackFrame = null;

			var match = regex.Match (line);
			if (!match.Success) {
				if (line.Trim ().StartsWith ("at ", StringComparison.InvariantCulture)) {
					stackFrame = new StackFrameData (line);
					return true;
				}
				return false;
			}

			string typeFullName, methodSignature;
			var methodStr = match.Groups ["Method"].Value.Trim ();
			if (!ExtractSignatures (methodStr, out typeFullName, out methodSignature))
				return false;

			var isILOffset = !string.IsNullOrEmpty (match.Groups ["IL"].Value);
			var offsetVarName = (isILOffset)? "IL" : "NativeOffset";
			var offset = int.Parse (match.Groups [offsetVarName].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

			uint methodIndex = 0xffffff;
			if (!string.IsNullOrEmpty (match.Groups ["MethodIndex"].Value))
				methodIndex = uint.Parse (match.Groups ["MethodIndex"].Value, CultureInfo.InvariantCulture);

			stackFrame = new StackFrameData (line, typeFullName, methodSignature, offset, isILOffset, methodIndex);

			return true;
		}

		static bool ExtractSignatures (string str, out string typeFullName, out string methodSignature)
		{
			var methodNameEnd = str.IndexOf ('(');
			if (methodNameEnd == -1) {
				typeFullName = methodSignature = null;
				return false;
			}

			var typeNameEnd = str.LastIndexOf ('.', methodNameEnd);
			if (typeNameEnd == -1) {
				typeFullName = methodSignature = null;
				return false;
			}

			// Adjustment for Type..ctor ()
			if (typeNameEnd > 0 && str [typeNameEnd - 1] == '.') {
				--typeNameEnd;
			}

			typeFullName = str.Substring (0, typeNameEnd);
			// Remove generic parameters
			typeFullName = Regex.Replace (typeFullName, @"\[[^\[\]]*\]", "");

			methodSignature = str.Substring (typeNameEnd + 1);

			return true;
		}

		internal void SetLocation (string file, int lineNumber)
		{
			File = file;
			LineNumber = lineNumber;
		}

		public override string ToString () {
			if (Line.Contains ("<filename unknown>:0") && LineNumber != -1)
				return Line.Replace ("<filename unknown>:0", string.Format ("{0}:{1}", File, LineNumber));

			return Line;
		}
	}
}
