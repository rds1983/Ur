using System;
using System.Collections.Generic;
using System.Text;
using ClangSharp;

namespace Ur
{
	public class ClangParser
	{
		public BaseProcessor Processor { get; private set; }

		public Dictionary<string, string> Process(ConversionParameters parameters)
		{
			if (parameters == null)
			{
				throw new ArgumentNullException("parameters");
			}

			var arr = new List<string>();

			foreach (var d in parameters.Defines)
			{
				arr.Add("-D" + d);
			}

			arr.Add(@"-IC:\Program Files (x86)\Microsoft Visual Studio\2017\Community\VC\Tools\MSVC\14.14.26428\include");

			var createIndex = clang.createIndex(0, 0);
			CXUnsavedFile unsavedFile;

			CXTranslationUnit tu;
			var res = clang.parseTranslationUnit2(createIndex,
				parameters.InputPath,
				arr.ToArray(),
				arr.Count,
				out unsavedFile,
				0,
				0,
				out tu);

			var numDiagnostics = clang.getNumDiagnostics(tu);
			for (uint i = 0; i < numDiagnostics; ++i)
			{
				var diag = clang.getDiagnostic(tu, i);
				var str =
					clang.formatDiagnostic(diag,
						(uint)
							(CXDiagnosticDisplayOptions.CXDiagnostic_DisplaySourceLocation |
							 CXDiagnosticDisplayOptions.CXDiagnostic_DisplaySourceRanges)).ToString();
				Logger.LogLine(str);
				clang.disposeDiagnostic(diag);
			}

			if (res != CXErrorCode.CXError_Success)
			{
				var sb = new StringBuilder();

				sb.AppendLine(res.ToString());

				numDiagnostics = clang.getNumDiagnostics(tu);
				for (uint i = 0; i < numDiagnostics; ++i)
				{
					var diag = clang.getDiagnostic(tu, i);
					sb.AppendLine(clang.getDiagnosticSpelling(diag).ToString());
					clang.disposeDiagnostic(diag);
				}

				throw new Exception(sb.ToString());
			}

			// Process
			Processor = new ConversionProcessor(parameters, tu);
			// Processor = new DumpProcessor(tu);
			Processor.Run();

			if (parameters.BeforeLastClosingBracket != null)
			{
				parameters.BeforeLastClosingBracket();
			}

			var result = new Dictionary<string, string>();
			var outputs = Processor.Outputs;

			foreach (var output in outputs)
			{
				result[output.Key] = output.Value.ToString();
			}

			clang.disposeTranslationUnit(tu);
			clang.disposeIndex(createIndex);


			return result;
		}
	}
}