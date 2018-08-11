﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClangSharp;
using SealangSharp;

namespace Ur
{
	internal class ConversionProcessor : BaseProcessor
	{
		private enum State
		{
			Structs,
			GlobalVariables,
			Enums,
			Functions
		}

		private readonly ConversionParameters _parameters;

		public ConversionParameters Parameters
		{
			get { return _parameters; }
		}

		private CXCursor _functionStatement;
		private CXType _returnType;
		private string _functionName;
		private readonly HashSet<string> _visitedStructs = new HashSet<string>();

		private readonly Dictionary<string, StructInfo> _structInfos = new Dictionary<string, StructInfo>();
		private readonly Dictionary<string, FunctionInfo> _functionInfos = new Dictionary<string, FunctionInfo>();

		private State _state;
		private readonly List<string> _items = new List<string>();
		private string _currentSource;
		private readonly Dictionary<string, StringWriter> _writers = new Dictionary<string, StringWriter>();
		private BaseConfig _currentStructConfig;
		private int _switchCount;
		private string _switchExpression;

		public override Dictionary<string, StringWriter> Outputs
		{
			get { return _writers; }
		}

		protected override TextWriter Writer
		{
			get
			{
				if (string.IsNullOrEmpty(_currentSource))
				{
					return null;
				}

				StringWriter sw;
				if (!_writers.TryGetValue(_currentSource, out sw))
				{
					sw = new StringWriter();
					_writers[_currentSource] = sw;

					if (_parameters.AddGeneratedByUr)
					{
						sw.WriteLine("// Generated by Ur at {0}", DateTime.Now);
						sw.WriteLine();
						sw.WriteLine("use std;");
						sw.WriteLine("use c_runtime;");
						sw.WriteLine();
					}
				}

				return sw;
			}
		}

		public ConversionProcessor(ConversionParameters parameters, CXTranslationUnit translationUnit) : base(translationUnit)
		{
			if (parameters == null)
			{
				throw new ArgumentNullException("parameters");
			}

			_parameters = parameters;

			Utility.TypeNameReplacer = n =>
			{
				StructInfo info;
				if (_structInfos.TryGetValue(n, out info))
				{
					n = info.Config.Name;
				}

				return n;
			};
		}

		private CXChildVisitResult VisitStructsPreprocess(CXCursor cursor, CXCursor parent, IntPtr data)
		{
			if (cursor.IsInSystemHeader())
			{
				return CXChildVisitResult.CXChildVisit_Continue;
			}

			var curKind = clang.getCursorKind(cursor);
			switch (curKind)
			{
				case CXCursorKind.CXCursor_UnionDecl:
				case CXCursorKind.CXCursor_StructDecl:
					var structName = clang.getCursorSpelling(cursor).ToString();

					// struct names can be empty, and so we visit its sibling to find the name
					if (string.IsNullOrEmpty(structName))
					{
						var forwardDeclaringVisitor = new ForwardDeclarationVisitor(cursor);
						clang.visitChildren(clang.getCursorSemanticParent(cursor), forwardDeclaringVisitor.Visit,
							new CXClientData(IntPtr.Zero));
						structName = clang.getCursorSpelling(forwardDeclaringVisitor.ForwardDeclarationCursor).ToString();

						if (string.IsNullOrEmpty(structName))
						{
							structName = "_";
						}
					}

					if (!_visitedStructs.Contains(structName))
					{
						Logger.Info("Prerocessing struct {0}", structName);

						if (_parameters.StructSource == null)
						{
							Logger.Warning("Skipping because ConversionParameters.StructSource is not set.");
						}

						var sc = _parameters.StructSource(structName);

						var info = new StructInfo
						{
							Name = structName,
							Config = sc
						};

						_structInfos[structName] = info;
					}

					return CXChildVisitResult.CXChildVisit_Continue;
			}

			return CXChildVisitResult.CXChildVisit_Recurse;
		}

		private void WriteClassStart(TextWriter sw, string cls)
		{
			sw.Write("struct {0} {{\n", cls);
		}

		private void WriteClassStart(string cls)
		{
			WriteClassStart(Writer, cls);
		}

		private CXChildVisitResult VisitStructs(CXCursor cursor, CXCursor parent, IntPtr data)
		{
			if (cursor.IsInSystemHeader())
			{
				return CXChildVisitResult.CXChildVisit_Continue;
			}

			var curKind = clang.getCursorKind(cursor);
			switch (curKind)
			{
				case CXCursorKind.CXCursor_UnionDecl:
				case CXCursorKind.CXCursor_StructDecl:
					var structName = clang.getCursorSpelling(cursor).ToString();

					// struct names can be empty, and so we visit its sibling to find the name
					if (string.IsNullOrEmpty(structName))
					{
						var forwardDeclaringVisitor = new ForwardDeclarationVisitor(cursor);
						clang.visitChildren(clang.getCursorSemanticParent(cursor), forwardDeclaringVisitor.Visit,
							new CXClientData(IntPtr.Zero));
						structName = clang.getCursorSpelling(forwardDeclaringVisitor.ForwardDeclarationCursor).ToString();

						if (string.IsNullOrEmpty(structName))
						{
							structName = "_";
						}
					}

					if (!_visitedStructs.Contains(structName) && cursor.GetChildrenCount() > 0)
					{
						Logger.Info("Processing struct {0}", structName);

						var info = _structInfos[structName];
						_currentStructConfig = info.Config;

						if (_currentStructConfig.Source == null || string.IsNullOrEmpty(_currentStructConfig.Source))
						{
							return CXChildVisitResult.CXChildVisit_Continue;
						}

						_currentSource = _currentStructConfig.Source;

						if (!string.IsNullOrEmpty(_currentStructConfig.Name))
						{
							WriteClassStart(_currentStructConfig.Name);
						}

						clang.visitChildren(cursor, VisitStructs, new CXClientData(IntPtr.Zero));

						if (!string.IsNullOrEmpty(_currentStructConfig.Name))
						{
							IndentedWriteLine("}");
						}

						WriteLine();

						_visitedStructs.Add(structName);
					}

					return CXChildVisitResult.CXChildVisit_Continue;
				case CXCursorKind.CXCursor_FieldDecl:
					var fieldName = clang.getCursorSpelling(cursor).ToString().FixSpecialWords();

					var expr = Process(cursor);

					var result = fieldName + ": " + expr.Info.RustType;

					/*					if (_currentStructConfig.StructType != StructType.Struct)
										{
											if (expr.Info.IsPointer && !string.IsNullOrEmpty(expr.Expression))
											{
												if (expr.Info.RecordType == RecordType.Struct || expr.Info.RecordType == RecordType.None)
												{
													result += " = new " + expr.Info.CsType + expr.Expression.Parentize();
												}
												else
												{
													result += " = new " + expr.Info.RecordName + "[" + expr.Expression + "]";
												}
											}
											else if (!expr.Info.IsPointer && expr.Info.RecordType != RecordType.None)
											{
												result += " = new " + expr.Info.CsType + "()";
											}
										}*/

					result += ",";
					IndentedWriteLine(result);

					return CXChildVisitResult.CXChildVisit_Continue;
			}

			return CXChildVisitResult.CXChildVisit_Recurse;
		}

		private CXChildVisitResult VisitEnums(CXCursor cursor, CXCursor parent, IntPtr data)
		{
			if (cursor.IsInSystemHeader())
			{
				return CXChildVisitResult.CXChildVisit_Continue;
			}

			var curKind = clang.getCursorKind(cursor);

			if (curKind == CXCursorKind.CXCursor_EnumDecl)
			{
				var enumName = clang.getCursorSpelling(cursor).ToString().Trim();

				if (string.IsNullOrEmpty(enumName))
				{
					var forwardDeclaringVisitor = new ForwardDeclarationVisitor(cursor);
					clang.visitChildren(clang.getCursorSemanticParent(cursor), forwardDeclaringVisitor.Visit,
						new CXClientData(IntPtr.Zero));
					enumName = clang.getCursorSpelling(forwardDeclaringVisitor.ForwardDeclarationCursor).ToString();
				}

				Logger.Info("Processing enum {0}", enumName);

				if (_parameters.EnumSource == null)
				{
					Logger.Warning("EnumSource is not set, therefore enum is skiped");
					return CXChildVisitResult.CXChildVisit_Continue;
				}

				var config = _parameters.EnumSource(enumName);

				if (config.Source == null || string.IsNullOrEmpty(config.Source))
				{
					return CXChildVisitResult.CXChildVisit_Continue;
				}

				_currentSource = config.Source;

				var i = 0;

				cursor.VisitWithAction(c =>
				{
					var name = clang.getCursorSpelling(c).ToString();
					var child = ProcessPossibleChildByIndex(c, 0);
					var value = i.ToString();
					if (child != null)
					{
						value = child.Expression;
						int parsed;
						if (child.Expression.TryParseNumber(out parsed))
						{
							i = parsed;
						}
					}

					var expr = "const " + name + ":i32 = " + value + ";";

					IndentedWriteLine(expr);
					i++;

					return CXChildVisitResult.CXChildVisit_Continue;
				});
			}

			return CXChildVisitResult.CXChildVisit_Continue;
		}

		private CXChildVisitResult VisitGlobalVariables(CXCursor cursor, CXCursor parent, IntPtr data)
		{
			if (cursor.IsInSystemHeader())
			{
				return CXChildVisitResult.CXChildVisit_Continue;
			}

			var curKind = clang.getCursorKind(cursor);
			var spelling = clang.getCursorSpelling(cursor).ToString();

			// look only at function decls
			if (curKind == CXCursorKind.CXCursor_VarDecl)
			{
				Logger.Info("Processing global variable {0}", spelling);

				if (Parameters.GlobalVariableSource == null)
				{
					Logger.Warning("GlobalVariableSource is not set, therefore skipping");
					return CXChildVisitResult.CXChildVisit_Continue;
				}

				var config = Parameters.GlobalVariableSource(spelling);

				if (string.IsNullOrEmpty(config.Source))
				{
					return CXChildVisitResult.CXChildVisit_Continue;
				}

				_currentSource = config.Source;

				var res = Process(cursor);

				if (Parameters.CustomGlobalVariableProcessor != null)
				{
					Parameters.CustomGlobalVariableProcessor(res);
				}

				if (!res.Expression.EndsWith(";"))
				{
					res.Expression += ";";
				}

				res.Expression = "const " + res.Expression;

				if (!string.IsNullOrEmpty(res.Expression))
				{
					IndentedWriteLine(res.Expression);
				}
			}

			return CXChildVisitResult.CXChildVisit_Continue;
		}

		private CXChildVisitResult VisitFunctionsPreprocess(CXCursor cursor, CXCursor parent, IntPtr data)
		{
			if (cursor.IsInSystemHeader())
			{
				return CXChildVisitResult.CXChildVisit_Continue;
			}

			var curKind = clang.getCursorKind(cursor);

			// first run - build _functionInfos
			if (curKind == CXCursorKind.CXCursor_FunctionDecl)
			{
				// Skip empty declarations
				var body = cursor.FindChild(CXCursorKind.CXCursor_CompoundStmt);
				if (!body.HasValue)
				{
					return CXChildVisitResult.CXChildVisit_Continue;
				}

				_functionStatement = body.Value;

				_functionName = clang.getCursorSpelling(cursor).ToString();

				Logger.Info("Prerocessing function {0}", _functionName);

				ProcessFunctionPreprocess(cursor);
			}

			return CXChildVisitResult.CXChildVisit_Recurse;
		}

		private CXChildVisitResult VisitFunctions(CXCursor cursor, CXCursor parent, IntPtr data)
		{
			if (cursor.IsInSystemHeader())
			{
				return CXChildVisitResult.CXChildVisit_Continue;
			}

			var curKind = clang.getCursorKind(cursor);

			// second run - code generation
			if (curKind == CXCursorKind.CXCursor_FunctionDecl)
			{
				// Skip empty declarations
				var body = cursor.FindChild(CXCursorKind.CXCursor_CompoundStmt);
				if (!body.HasValue)
				{
					return CXChildVisitResult.CXChildVisit_Continue;
				}

				_functionStatement = body.Value;

				_functionName = clang.getCursorSpelling(cursor).ToString();

				var fc = _functionInfos[_functionName].Config;
				if (fc.Source == null || string.IsNullOrEmpty(fc.Source))
				{
					return CXChildVisitResult.CXChildVisit_Continue;
				}

				_currentSource = fc.Source;

				Logger.Info("Processing function {0}", _functionName);

				ProcessFunction(cursor);
			}

			return CXChildVisitResult.CXChildVisit_Recurse;
		}

		private CursorProcessResult ProcessChildByIndex(CXCursor cursor, int index)
		{
			return Process(cursor.EnsureChildByIndex(index));
		}

		private CursorProcessResult ProcessPossibleChildByIndex(CXCursor cursor, int index)
		{
			var childCursor = cursor.GetChildByIndex(index);
			if (childCursor == null)
			{
				return null;
			}

			return Process(childCursor.Value);
		}

		internal void AppendGZ(CursorProcessResult crp)
		{
			var info = crp.Info;

			if (info.Kind == CXCursorKind.CXCursor_BinaryOperator)
			{
				var type = sealang.cursor_getBinaryOpcode(info.Cursor);
				if (type != BinaryOperatorKind.Or && type != BinaryOperatorKind.And)
				{
					return;
				}
			}

			if (info.Kind == CXCursorKind.CXCursor_ParenExpr)
			{
				var child2 = ProcessChildByIndex(info.Cursor, 0);
				if (child2.Info.Kind == CXCursorKind.CXCursor_BinaryOperator &&
					sealang.cursor_getBinaryOpcode(child2.Info.Cursor).IsBinaryOperator())
				{
					var sub = ProcessChildByIndex(crp.Info.Cursor, 0);
					crp.Expression = sub.Expression.Parentize() + "!= 0";
				}
				return;
			}

			if (info.Kind == CXCursorKind.CXCursor_UnaryOperator)
			{
				var child = ProcessChildByIndex(info.Cursor, 0);
				var type = sealang.cursor_getUnaryOpcode(info.Cursor);
				if (child.Info.IsPointer)
				{
					if (type == UnaryOperatorKind.LNot)
					{
						crp.Expression = child.Expression + "== std::ptr::null_mut()";
					}

					return;
				}

				if (child.Info.Kind == CXCursorKind.CXCursor_ParenExpr)
				{
					var child2 = ProcessChildByIndex(child.Info.Cursor, 0);
					if (child2.Info.Kind == CXCursorKind.CXCursor_BinaryOperator &&
						sealang.cursor_getBinaryOpcode(child2.Info.Cursor).IsBinaryOperator())
					{
					}
					else
					{
						return;
					}
				}

				if (type == UnaryOperatorKind.LNot)
				{
					var sub = ProcessChildByIndex(crp.Info.Cursor, 0);
					crp.Expression = sub.Expression + "== 0";

					return;
				}
			}

			if (info.Type.kind.IsPrimitiveNumericType())
			{
				crp.Expression = crp.Expression.Parentize() + " != 0";
			}

			if (info.Type.IsPointer())
			{
				crp.Expression = crp.Expression.Parentize() + " != std::ptr::null_mut()";
			}
		}

		/*		private string ReplaceNullWithPointerByte(string expr)
				{
					if (expr == "null" || expr == "(null)")
					{
						return "Pointer<byte>.Null";
					}

					return expr;
				}


				private string ReplaceNullWithPointerByte2(string expr, string type)
				{
					if (expr == "null" || expr == "(null)" || expr == "0" || expr == "(0)")
					{
						return type + ".Null";
					}

					return expr;
				}*/

		private string ReplaceCommas(CursorProcessResult info)
		{
			var executionExpr = info.GetExpression();
			if (info != null && info.Info.Kind == CXCursorKind.CXCursor_BinaryOperator)
			{
				var type = sealang.cursor_getBinaryOpcode(info.Info.Cursor);
				if (type == BinaryOperatorKind.Comma)
				{
					var a = ReplaceCommas(ProcessChildByIndex(info.Info.Cursor, 0));
					var b = ReplaceCommas(ProcessChildByIndex(info.Info.Cursor, 1));

					executionExpr = a + ";" + b;
				}
			}

			return executionExpr;
		}

		private string InternalProcess(CursorInfo info)
		{
			switch (info.Kind)
			{
				case CXCursorKind.CXCursor_EnumConstantDecl:
					{
						var expr = ProcessPossibleChildByIndex(info.Cursor, 0);

						return info.Spelling + " = " + expr.Expression;
					}

				case CXCursorKind.CXCursor_UnaryExpr:
					{
						var opCode = sealang.cursor_getUnaryOpcode(info.Cursor);
						var expr = ProcessPossibleChildByIndex(info.Cursor, 0);

						string[] tokens = null;

						if ((int)opCode == 99999 && expr != null)
						{
							tokens = info.Cursor.Tokenize(_translationUnit);
							var op = "std::mem::size_of";
							if (tokens.Length > 0 && tokens[0] == "__alignof")
							{
								// 4 is default alignment
								return "4";
							}

							if (!string.IsNullOrEmpty(expr.Expression))
							{
								// sizeof
								return op + "(" + expr.Expression + ")";
							}

							if (expr.Info.Kind == CXCursorKind.CXCursor_TypeRef)
							{
								return op + "::<" + expr.Info.RustType + ">()";
							}
						}

						if (tokens == null)
						{
							tokens = info.Cursor.Tokenize(_translationUnit);
						}

						return string.Join(string.Empty, tokens);
					}
				case CXCursorKind.CXCursor_DeclRefExpr:
					{
						var result = info.Spelling.FixSpecialWords();

						FunctionInfo functionInfo;

						if (_functionInfos.TryGetValue(result, out functionInfo))
						{
							result = functionInfo.Config.Name;
						}

						return result;
					}
				case CXCursorKind.CXCursor_CompoundAssignOperator:
				case CXCursorKind.CXCursor_BinaryOperator:
					{
						var a = ProcessChildByIndex(info.Cursor, 0);
						var b = ProcessChildByIndex(info.Cursor, 1);
						var type = sealang.cursor_getBinaryOpcode(info.Cursor);

						if (type.IsLogicalBinaryOperator())
						{
							AppendGZ(a);
							AppendGZ(b);
						}

/*						if (type.IsLogicalBooleanOperator())
						{
							a.Expression = a.Expression.Parentize();
							b.Expression = b.Expression.Parentize();
						}*/

						if (type.IsAssign() && type != BinaryOperatorKind.ShlAssign && type != BinaryOperatorKind.ShrAssign)
						{
							// Explicity cast right to left
							if (!info.Type.IsPointer())
							{
								if (b.Info.Kind == CXCursorKind.CXCursor_ParenExpr && b.Info.Cursor.GetChildrenCount() > 0)
								{
									var bb = ProcessChildByIndex(b.Info.Cursor, 0);
									if (bb.Info.Kind == CXCursorKind.CXCursor_BinaryOperator &&
										sealang.cursor_getBinaryOpcode(bb.Info.Cursor).IsLogicalBooleanOperator())
									{
										b = bb;
									}
								}

//								b.Expression = b.Expression.ApplyCast(info.RustType);
							}
						}

						if (a.Info.IsPointer)
						{
							switch (type)
							{
								case BinaryOperatorKind.Add:
									return a.Expression + "[" + b.Expression + "]";
							}
						}

						if (a.Info.IsPointer && (type == BinaryOperatorKind.Assign || type.IsBooleanOperator()) &&
							(b.Expression.Deparentize() == "0"))
						{
							b.Expression = "std::ptr::null_mut()";
						}

						var str = sealang.cursor_getOperatorString(info.Cursor);
						var result = a.Expression + " " + str + " " + b.Expression;

						if (type.IsAssign())
						{
							result = result + ";";
						}

						return result;
					}
				case CXCursorKind.CXCursor_UnaryOperator:
					{
						var a = ProcessChildByIndex(info.Cursor, 0);

						var type = sealang.cursor_getUnaryOpcode(info.Cursor);
						var str = sealang.cursor_getOperatorString(info.Cursor).ToString();

						if ((type == UnaryOperatorKind.AddrOf || type == UnaryOperatorKind.Deref))
						{
							str = string.Empty;
						}


						if (type == UnaryOperatorKind.PreInc || type == UnaryOperatorKind.PostInc)
						{
							return a.Expression + " += 1";
						}

						if (type == UnaryOperatorKind.PreDec || type == UnaryOperatorKind.PostDec)
						{
							return a.Expression + " -= 1";
						}

						if (type == UnaryOperatorKind.Not)
						{
							str = "!";
						}

						var left = type.IsUnaryOperatorPre();
						if (left)
						{
							return str + a.Expression;
						}

						return a.Expression + str;
					}

				case CXCursorKind.CXCursor_CallExpr:
					{
						var size = info.Cursor.GetChildrenCount();

						var functionExpr = ProcessChildByIndex(info.Cursor, 0);
						var functionName = functionExpr.Expression.Deparentize();

						FunctionInfo functionInfo;
						if (!_functionInfos.TryGetValue(functionName, out functionInfo))
						{
							var fn = functionName;

							functionInfo =
								(from f in _functionInfos where f.Value.Config.Name == fn select f.Value)
									.FirstOrDefault();
						}

						if (functionInfo != null)
						{
							functionName = functionInfo.Config.Name;
						}

						// Retrieve arguments
						var args = new List<string>();
						for (var i = 1; i < size; ++i)
						{
							var argExpr = ProcessChildByIndex(info.Cursor, i);

/*							if (!argExpr.Info.IsPointer)
							{
								argExpr.Expression = argExpr.Expression.ApplyCast(argExpr.Info.RustType);
							}
							else if (argExpr.Expression.Deparentize() == "0")
							{
								argExpr.Expression = "null";
							}*/

							args.Add(argExpr.Expression);
						}

						var sb = new StringBuilder();

						sb.Append(functionName + "(");
						sb.Append(string.Join(", ", args));
						sb.Append(")");

						return sb.ToString();
					}
				case CXCursorKind.CXCursor_ReturnStmt:
					{
						var child = ProcessPossibleChildByIndex(info.Cursor, 0);

						var ret = child.GetExpression();

						if (_returnType.kind != CXTypeKind.CXType_Void)
						{
							if (!_returnType.IsPointer())
							{
/*								if (child != null && child.Info.Kind == CXCursorKind.CXCursor_BinaryOperator &&
									sealang.cursor_getBinaryOpcode(child.Info.Cursor).IsLogicalBooleanOperator())
								{
									ret = "(" + ret + "?1:0)";
								}*/

								return "return " + ret;
							}
						}

						if (_returnType.IsPointer() && ret == "0")
						{
							ret = "std::ptr::null_mut()";
						}

						var exp = string.IsNullOrEmpty(ret) ? "return" : "return " + ret;

						return exp;
					}
				case CXCursorKind.CXCursor_IfStmt:
					{
						var conditionExpr = ProcessChildByIndex(info.Cursor, 0);
						AppendGZ(conditionExpr);

						var executionExpr = ProcessChildByIndex(info.Cursor, 1);
						var elseExpr = ProcessPossibleChildByIndex(info.Cursor, 2);

						if (executionExpr != null && !string.IsNullOrEmpty(executionExpr.Expression))
						{
							executionExpr.Expression = executionExpr.Expression.EnsureStatementFinished().Curlize();
						}

						var expr = "if " + conditionExpr.Expression + " " + executionExpr.Expression;

						if (elseExpr != null)
						{
							expr += " else " + elseExpr.Expression.EnsureStatementFinished().Curlize();
						}

						return expr;
					}
				case CXCursorKind.CXCursor_ForStmt:
					{
						var size = info.Cursor.GetChildrenCount();

						CursorProcessResult execution = null, start = null, condition = null, it = null;
						switch (size)
						{
							case 1:
								execution = ProcessChildByIndex(info.Cursor, 0);
								break;
							case 2:
								start = ProcessChildByIndex(info.Cursor, 0);
								condition = ProcessChildByIndex(info.Cursor, 1);
								break;
							case 3:
								var expr = ProcessChildByIndex(info.Cursor, 0);
								if (expr.Info.Kind == CXCursorKind.CXCursor_BinaryOperator &&
									sealang.cursor_getBinaryOpcode(expr.Info.Cursor).IsBooleanOperator())
								{
									condition = expr;
								}
								else
								{
									start = expr;
								}

								expr = ProcessChildByIndex(info.Cursor, 1);
								if (expr.Info.Kind == CXCursorKind.CXCursor_BinaryOperator &&
									sealang.cursor_getBinaryOpcode(expr.Info.Cursor).IsBooleanOperator())
								{
									condition = expr;
								}
								else
								{
									it = expr;
								}

								execution = ProcessChildByIndex(info.Cursor, 2);
								break;
							case 4:
								start = ProcessChildByIndex(info.Cursor, 0);
								condition = ProcessChildByIndex(info.Cursor, 1);
								it = ProcessChildByIndex(info.Cursor, 2);
								execution = ProcessChildByIndex(info.Cursor, 3);
								break;
						}

						
						var executionExpr = ReplaceCommas(execution);
						executionExpr = executionExpr.EnsureStatementFinished();

						var startExpr = start.GetExpression().Replace(",", ";");
						var itExpr = it.GetExpression().Replace(",", ";");

						if (execution.Info.Kind == CXCursorKind.CXCursor_CompoundStmt)
						{
							var openingBracketIndex = executionExpr.IndexOf('{');

							if (openingBracketIndex != -1)
							{
								executionExpr = executionExpr.Substring(0, openingBracketIndex + 1) + itExpr + ";\n" + executionExpr.Substring(openingBracketIndex + 1);

								return startExpr + ";\n" + "while (" + condition.GetExpression() + ") " + executionExpr;
							}
						}

						return startExpr + ";\n" + "while (" + condition.GetExpression() + ") {\n" + itExpr + ";\n" +
							   executionExpr + "}";
					}

				case CXCursorKind.CXCursor_CaseStmt:
					{
						var expr = ProcessChildByIndex(info.Cursor, 0);
						var execution = ProcessChildByIndex(info.Cursor, 1);

						var s2 = "if ";

						if (_switchCount > 0)
						{
							s2 = "} else " + s2;
						}

						++_switchCount;

						return s2 + _switchExpression + " == " + expr.Expression + " {" + execution.Expression;
					}

				case CXCursorKind.CXCursor_DefaultStmt:
					{
						var execution = ProcessChildByIndex(info.Cursor, 0);

						var s2 = "else { ";
						if (_switchCount > 0)
						{
							s2 = "} " + s2;
						}

						++_switchCount;

						return s2 + execution.Expression;
					}

				case CXCursorKind.CXCursor_SwitchStmt:
					{
						_switchCount = 0;
						_switchExpression = ProcessChildByIndex(info.Cursor, 0).Expression;
						var execution = ProcessChildByIndex(info.Cursor, 1);
						return execution.Expression + "}";
					}

				case CXCursorKind.CXCursor_DoStmt:
					{
						var execution = ProcessChildByIndex(info.Cursor, 0);
						var expr = ProcessChildByIndex(info.Cursor, 1);
						AppendGZ(expr);

						var exeuctionExpr = execution.Expression.EnsureStatementFinished();

						var breakExpr = "if !(" + expr.Expression + ") {break;}";

						if (execution.Info.Kind == CXCursorKind.CXCursor_CompoundStmt)
						{
							var closingBracketIndex = exeuctionExpr.LastIndexOf("}");
							if (closingBracketIndex != -1)
							{
								return "while(true) " + exeuctionExpr.Substring(0, closingBracketIndex) +
									breakExpr + exeuctionExpr.Substring(closingBracketIndex);
							}
						}

						return "while(true) {" + execution.Expression.EnsureStatementFinished() + breakExpr + "}";
					}

				case CXCursorKind.CXCursor_WhileStmt:
					{
						var expr = ProcessChildByIndex(info.Cursor, 0);
						AppendGZ(expr);
						var execution = ProcessChildByIndex(info.Cursor, 1);

						return "while (" + expr.Expression + ") " + execution.Expression.EnsureStatementFinished().Curlize();
					}

				case CXCursorKind.CXCursor_LabelRef:
					return info.Spelling;
				case CXCursorKind.CXCursor_GotoStmt:
					{
						var label = ProcessChildByIndex(info.Cursor, 0);

						return "goto " + label.Expression;
					}

				case CXCursorKind.CXCursor_LabelStmt:
					{
						var sb = new StringBuilder();

						sb.Append(info.Spelling);
						sb.Append(":;\n");

						var size = info.Cursor.GetChildrenCount();
						for (var i = 0; i < size; ++i)
						{
							var child = ProcessChildByIndex(info.Cursor, i);
							sb.Append(child.Expression);
						}

						return sb.ToString();
					}

				case CXCursorKind.CXCursor_ConditionalOperator:
					{
						var condition = ProcessChildByIndex(info.Cursor, 0);

						var a = ProcessChildByIndex(info.Cursor, 1);
						var b = ProcessChildByIndex(info.Cursor, 2);

/*						if (condition.Info.IsPrimitiveNumericType)
						{
							var gz = true;

							if (condition.Info.Kind == CXCursorKind.CXCursor_ParenExpr)
							{
								gz = false;
							}
							else if (condition.Info.Kind == CXCursorKind.CXCursor_BinaryOperator)
							{
								var op = sealang.cursor_getBinaryOpcode(condition.Info.Cursor);

								if (op == BinaryOperatorKind.Or || op == BinaryOperatorKind.And)
								{
								}
								else
								{
									gz = false;
								}
							}

							if (gz)
							{
								condition.Expression = condition.Expression.Parentize() + " != 0";
							}
						}*/

						return "if " + condition.Expression + "{" + a.Expression + "} else {" + b.Expression + "}";
					}
				case CXCursorKind.CXCursor_MemberRefExpr:
					{
						var a = ProcessChildByIndex(info.Cursor, 0);

						var op = ".";

						var result = a.Expression + op + info.Spelling.FixSpecialWords();

						return result;
					}
				case CXCursorKind.CXCursor_IntegerLiteral:
					{
						var tokens = info.Cursor.Tokenize(_translationUnit);
						if (tokens.Length == 0)
						{
							return sealang.cursor_getLiteralString(info.Cursor).ToString();
						}

						var t = tokens[0].Replace("U", "");
						t = t.Replace("u", "");

						return t;
					}
				case CXCursorKind.CXCursor_FloatingLiteral:
					{
						{
							var tokens = info.Cursor.Tokenize(_translationUnit);
							if (tokens.Length == 0)
							{
								return sealang.cursor_getLiteralString(info.Cursor).ToString();
							}

							return tokens[0].Replace("f", "f32");
						}
					}
				case CXCursorKind.CXCursor_CharacterLiteral:
					var s = sealang.cursor_getLiteralString(info.Cursor).ToString();
					if (string.IsNullOrEmpty(s) || (s.Length == 1 && s[0] == '\0'))
					{
						s = "\\0";
					}

					return "'" + s + "'";
				case CXCursorKind.CXCursor_StringLiteral:
					return info.Spelling.StartsWith("L") ? info.Spelling.Substring(1) : info.Spelling;
				case CXCursorKind.CXCursor_VarDecl:
					{
						CursorProcessResult rvalue = null;
						var size = info.Cursor.GetChildrenCount();

						var name = info.Spelling.FixSpecialWords();

						if (size > 0)
						{
							rvalue = ProcessPossibleChildByIndex(info.Cursor, size - 1);

							if (info.Type.IsArray())
							{
								var arrayType = info.Type.GetPointeeType().ToCSharpTypeString();
								if (_state == State.Functions)
								{
									info.RustType = info.Type.ToCSharpTypeString(true);
								}

								var t = info.Type.GetPointeeType().ToCSharpTypeString();

								if (rvalue.Info.Kind == CXCursorKind.CXCursor_TypeRef || rvalue.Info.Kind == CXCursorKind.CXCursor_IntegerLiteral ||
									rvalue.Info.Kind == CXCursorKind.CXCursor_BinaryOperator)
								{
									string sizeExp;
									if (rvalue.Info.Kind == CXCursorKind.CXCursor_TypeRef ||
										rvalue.Info.Kind == CXCursorKind.CXCursor_IntegerLiteral)
									{
										sizeExp = info.Type.GetArraySize().ToString();
									}
									else
									{
										sizeExp = rvalue.Expression;
									}

									if (_state != State.Functions)
									{
										rvalue.Expression = "new " + t + "[" + sizeExp + "]";
									}
									else
									{
										rvalue.Expression = "unsafe {std::mem::uninitialized()}";
									}
								}
							}
						}

						var expr = name + ":" + info.RustType;

						if (info.Type.IsArray())
						{
							expr = name + ":[" + info.Type.GetPointeeType().ToCSharpTypeString() + ";" + info.Type.GetArraySize() + "]";
						}

						if (rvalue != null && !string.IsNullOrEmpty(rvalue.Expression))
						{
							if (!info.IsPointer && !info.IsArray)
							{
								expr += " = ";
								expr += rvalue.Expression;
							}
							else
							{
								var t = info.Type.GetPointeeType().ToCSharpTypeString();
								if (rvalue.Info.Kind == CXCursorKind.CXCursor_InitListExpr)
								{
									if (_state != State.Functions)
									{
									}
									else
									{
/*										var arrayType = info.Type.GetPointeeType().ToCSharpTypeString();

										rvalue.Expression = "stackalloc " + arrayType + "[" + info.Type.GetArraySize() + "];\n";
										var size2 = rvalue.Info.Cursor.GetChildrenCount();
										for (var i = 0; i < size2; ++i)
										{
											var exp = ProcessChildByIndex(rvalue.Info.Cursor, i);

											if (!exp.Info.IsPointer)
											{
												exp.Expression = exp.Expression.ApplyCast(exp.Info.RustType);
											}

											rvalue.Expression += name + "[" + i + "] = " + exp.Expression + ";\n";
										}*/
									}
								}

/*								if (info.IsPointer && !info.IsArray && rvalue.Info.IsArray &&
									rvalue.Info.Type.GetPointeeType().kind.IsPrimitiveNumericType() &&
									rvalue.Info.Kind != CXCursorKind.CXCursor_StringLiteral)
								{
									rvalue.Expression = "((" + info.Type.GetPointeeType().ToCSharpTypeString() + "*)" + rvalue.Expression + ")";
								}*/

								if (info.IsPointer && !info.IsArray && rvalue.Expression == "0")
								{
									rvalue.Expression = "std::ptr::null_mut()";
								}

								if (info.IsArray)
								{
									rvalue.Expression = "unsafe {std::mem::uninitialized()}";
								}

								expr += " = " + rvalue.Expression;
							}
						}
						else if (!info.IsPointer)
						{
//							expr += " =  new " + info.RustType + "()";
						}

						if (_state == State.Functions)
						{
							if (!info.IsPointer || info.IsArray)
							{
								expr = "mut " + expr;
							}

							expr = "let " + expr;
						}

						expr = expr + ";";

						return expr;
					}
				case CXCursorKind.CXCursor_DeclStmt:
					{
						var sb = new StringBuilder();
						var size = info.Cursor.GetChildrenCount();
						for (var i = 0; i < size; ++i)
						{
							var exp = ProcessChildByIndex(info.Cursor, i);
							exp.Expression = exp.Expression.EnsureStatementFinished();
							sb.Append(exp.Expression);
						}

						return sb.ToString();
					}
				case CXCursorKind.CXCursor_CompoundStmt:
					{
						var sb = new StringBuilder();
						sb.Append("{\n");

						var size = info.Cursor.GetChildrenCount();
						for (var i = 0; i < size; ++i)
						{
							var exp = ProcessChildByIndex(info.Cursor, i);
							exp.Expression = exp.Expression.EnsureStatementFinished();
							sb.Append(exp.Expression);
						}

						sb.Append("}\n");

						var fullExp = sb.ToString();

						return fullExp;
					}

				case CXCursorKind.CXCursor_ArraySubscriptExpr:
					{
						var var = ProcessChildByIndex(info.Cursor, 0);
						var expr = ProcessChildByIndex(info.Cursor, 1);

						return var.Expression + "[" + expr.Expression + "]";
					}

				case CXCursorKind.CXCursor_InitListExpr:
					{
						var sb = new StringBuilder();

						sb.Append("[ ");
						var size = info.Cursor.GetChildrenCount();
						for (var i = 0; i < size; ++i)
						{
							var exp = ProcessChildByIndex(info.Cursor, i);

							sb.Append(exp.Expression);

							if (i < size - 1)
							{
								sb.Append(", ");
							}
						}

						sb.Append(" ]");
						return sb.ToString();
					}

				case CXCursorKind.CXCursor_ParenExpr:
					{
						var expr = ProcessPossibleChildByIndex(info.Cursor, 0);
						var e = expr.GetExpression();

/*						if (info.RustType != expr.Info.RustType)
						{
							e = e.ApplyCast(info.RustType);
						}
						else
						{
							e = e.Parentize();
						}*/

						return e;
					}

				case CXCursorKind.CXCursor_BreakStmt:
					return ",";
				case CXCursorKind.CXCursor_ContinueStmt:
					return "continue";

				case CXCursorKind.CXCursor_CStyleCastExpr:
					{
						var size = info.Cursor.GetChildrenCount();
						var child = ProcessChildByIndex(info.Cursor, size - 1);

						var expr = child.Expression;

/*						if (info.RustType != child.Info.RustType)
						{
							expr = expr.ApplyCast(info.RustType);
						}*/

						return expr;
					}

				case CXCursorKind.CXCursor_UnexposedExpr:
					{
						// Return last child
						var size = info.Cursor.GetChildrenCount();

						if (size == 0)
						{
							return string.Empty;
						}

						var expr = ProcessPossibleChildByIndex(info.Cursor, size - 1);

						if (info.IsPointer && expr.Expression.Deparentize() == "0")
						{
							expr.Expression = "std::ptr::null_mut()";
						}

						if (info.IsPointer && expr.Info.IsArray)
						{
							// expr.Expression += ".as_mut_ptr()";
						}

						return expr.Expression;
					}

				default:
					{
						// Return last child
						var size = info.Cursor.GetChildrenCount();

						if (size == 0)
						{
							return string.Empty;
						}

						var expr = ProcessPossibleChildByIndex(info.Cursor, size - 1);

						return expr.GetExpression();
					}
			}
		}

		private CursorProcessResult Process(CXCursor cursor)
		{
			var info = new CursorInfo(cursor);

			var expr = InternalProcess(info);

			return new CursorProcessResult(info)
			{
				Expression = expr
			};
		}

		private CXChildVisitResult VisitFunctionBody(CXCursor cursor, CXCursor parent, IntPtr data)
		{
			var res = Process(cursor);

			if (!string.IsNullOrEmpty(res.Expression))
			{
				IndentedWriteLine(res.Expression.EnsureStatementFinished());
			}

			return CXChildVisitResult.CXChildVisit_Continue;
		}

		private void ProcessFunctionPreprocess(CXCursor cursor)
		{
			var functionType = clang.getCursorType(cursor);
			var functionName = clang.getCursorSpelling(cursor).ToString();
			_returnType = clang.getCursorResultType(cursor).Desugar();

			var numArgTypes = clang.getNumArgTypes(functionType);

			_functionInfos[functionName] = new FunctionInfo
			{
				Name = functionName
			};

			var sb = new StringBuilder();
			for (uint i = 0; i < numArgTypes; ++i)
			{
				var paramCursor = clang.Cursor_getArgument(cursor, i);
				var spelling = clang.getCursorSpelling(paramCursor).ToString();
				var type = clang.getArgType(functionType, i);

				var name = spelling.FixSpecialWords();
				var typeName = type.ToCSharpTypeString(true);

				sb.Append(typeName);
				sb.Append(" ");
				sb.Append(name);

				if (i < numArgTypes - 1)
				{
					sb.Append(", ");
				}
			}

			_functionInfos[functionName].Signature = sb.ToString();

			BaseConfig fc;

			if (Parameters.FunctionSource == null)
			{
				fc = new BaseConfig
				{
					Name = _functionName,
					Source = null
				};
			}
			else
			{
				fc = Parameters.FunctionSource(_functionInfos[_functionName]);
			}

			_functionInfos[_functionName].Config = fc;
		}

		private void ProcessFunction(CXCursor cursor)
		{
			WriteFunctionStart(cursor);

			_indentLevel++;

			clang.visitChildren(_functionStatement, VisitFunctionBody, new CXClientData(IntPtr.Zero));

			_indentLevel--;

			IndentedWriteLine("}");

			WriteLine();
		}

		private void WriteFunctionStart(CXCursor cursor)
		{
			var functionType = clang.getCursorType(cursor);
			var functionName = clang.getCursorSpelling(cursor).ToString();
			_returnType = clang.getCursorResultType(cursor).Desugar();

			var info = _functionInfos[functionName];

			IndentedWrite("unsafe fn ");

			Write(info.Config.Name);
			Write("(");

			_items.Clear();


			var numArgTypes = clang.getNumArgTypes(functionType);

			var first = true;
			for (var i = (uint)0; i < numArgTypes; ++i)
			{
				if (!first)
				{
					Write(", ");
				}

				ArgumentHelper(functionType, clang.Cursor_getArgument(cursor, i), i);
				first = false;
			}

			Write(")");

			if (_returnType.kind != CXTypeKind.CXType_Void)
			{
				Write(" -> " + _returnType.ToCSharpTypeString() + " ");
			}

			IndentedWriteLine("{");

			if (Parameters.FunctionHeaderProcessed != null)
			{
				Parameters.FunctionHeaderProcessed(info.Config.Name, _items.ToArray());
			}
		}

		private void ArgumentHelper(CXType functionType, CXCursor paramCursor, uint index)
		{
			var type = clang.getArgType(functionType, index);

			var spelling = clang.getCursorSpelling(paramCursor).ToString();

			var name = spelling.FixSpecialWords();
			var typeName = type.ToCSharpTypeString(true);

			var sb = new StringBuilder();

			sb.Append(name);
			sb.Append(":");
			sb.Append(typeName);


			_items.Add(sb.ToString());

			Write(sb.ToString());
		}

		public override void Run()
		{
			_state = State.Enums;
			clang.visitChildren(clang.getTranslationUnitCursor(_translationUnit), VisitEnums, new CXClientData(IntPtr.Zero));

			_state = State.GlobalVariables;
			clang.visitChildren(clang.getTranslationUnitCursor(_translationUnit), VisitGlobalVariables,
				new CXClientData(IntPtr.Zero));

			_state = State.Structs;
			clang.visitChildren(clang.getTranslationUnitCursor(_translationUnit), VisitStructsPreprocess,
				new CXClientData(IntPtr.Zero));
			clang.visitChildren(clang.getTranslationUnitCursor(_translationUnit), VisitStructs, new CXClientData(IntPtr.Zero));

			_state = State.Functions;
			clang.visitChildren(clang.getTranslationUnitCursor(_translationUnit), VisitFunctionsPreprocess,
				new CXClientData(IntPtr.Zero));
			clang.visitChildren(clang.getTranslationUnitCursor(_translationUnit), VisitFunctions, new CXClientData(IntPtr.Zero));
		}
	}
}