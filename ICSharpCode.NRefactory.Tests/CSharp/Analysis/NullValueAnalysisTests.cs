//
// NullValueAnalysisTests.cs
//
// Author:
//       Luís Reis <luiscubal@gmail.com
//
// Copyright (c) 2013 Luís Reis
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using NUnit.Framework;
using ICSharpCode.NRefactory.CSharp.Resolver;
using ICSharpCode.NRefactory.TypeSystem.Implementation;
using System.Linq;
using ICSharpCode.NRefactory.TypeSystem;
using System.Threading;
using ICSharpCode.NRefactory.CSharp.TypeSystem;
using ICSharpCode.NRefactory.CSharp;

namespace ICSharpCode.NRefactory.CSharp.Analysis
{
	[TestFixture]
	public class NullValueAnalysisTests
	{
		NullValueAnalysis CreateNullValueAnalysis(SyntaxTree tree, MethodDeclaration methodDeclaration)
		{
			IProjectContent pc = new CSharpProjectContent();
			pc = pc.AddAssemblyReferences(CecilLoaderTests.Mscorlib);
			pc = pc.AddOrUpdateFiles(new[] { tree.ToTypeSystem() });
			var compilation = pc.CreateCompilation();
			var resolver = new CSharpResolver(compilation);
			var astResolver = new CSharpAstResolver(resolver, tree);
			return new NullValueAnalysis(methodDeclaration, astResolver, CancellationToken.None);
		}

		NullValueAnalysis CreateNullValueAnalysis(MethodDeclaration methodDeclaration)
		{
			var type = new TypeDeclaration {
				Name = "DummyClass",
				ClassType = ClassType.Class
			};
			type.Members.Add(methodDeclaration);
			var tree = new SyntaxTree() { FileName = "test.cs" };
			tree.Members.Add(type);

			return CreateNullValueAnalysis(tree, methodDeclaration);
		}

		ParameterDeclaration CreatePrimitiveParameter(string typeKeyword, string parameterName)
		{
			return new ParameterDeclaration(new PrimitiveType(typeKeyword), parameterName);
		}

		ParameterDeclaration CreateStringParameter(string parameterName = "p")
		{
			return CreatePrimitiveParameter("string", parameterName);
		}

		[Test]
		public void TestSimple()
		{
			var method = new MethodDeclaration {
				Body = new BlockStatement {
					new ExpressionStatement(new AssignmentExpression(
						new IdentifierExpression("p"), new NullReferenceExpression())),
					new ExpressionStatement(new AssignmentExpression(
						new IdentifierExpression("p"), new PrimitiveExpression("Hello"))),
					new ReturnStatement()
				}
			};
			method.Parameters.Add(CreateStringParameter());

			var analysis = CreateNullValueAnalysis(method);
			var stmt1 = method.Body.Statements.First();
			var stmt2 = method.Body.Statements.ElementAt(1);
			
			Assert.AreEqual(NullValueStatus.PotentiallyNull, analysis.GetVariableStatusBeforeStatement(stmt1, "p"));
			Assert.AreEqual(NullValueStatus.DefinitelyNull, analysis.GetVariableStatusBeforeStatement(stmt2, "p"));
			Assert.AreEqual(NullValueStatus.DefinitelyNotNull, analysis.GetVariableStatusAfterStatement(stmt2, "p"));
		}

		[Test]
		public void TestIfStatement()
		{
			var method = new MethodDeclaration {
				Body = new BlockStatement {
					new IfElseStatement {
						Condition = new BinaryOperatorExpression(new IdentifierExpression("p"),
						                                         BinaryOperatorType.Equality,
						                                         new NullReferenceExpression()),
						TrueStatement = new ExpressionStatement(new AssignmentExpression(
							new IdentifierExpression("p"),
							new PrimitiveExpression("Hello")))
					},
					new ReturnStatement()
				}
			};
			method.Parameters.Add(CreateStringParameter());

			var analysis = CreateNullValueAnalysis(method);
			var stmt1 = (IfElseStatement)method.Body.Statements.First();
			var stmt2 = (ExpressionStatement)stmt1.TrueStatement;
			var stmt3 = (ReturnStatement)method.Body.Statements.ElementAt(1);

			Assert.AreEqual(NullValueStatus.PotentiallyNull, analysis.GetVariableStatusBeforeStatement(stmt1, "p"));
			Assert.AreEqual(NullValueStatus.DefinitelyNull, analysis.GetVariableStatusBeforeStatement(stmt2, "p"));
			Assert.AreEqual(NullValueStatus.DefinitelyNotNull, analysis.GetVariableStatusAfterStatement(stmt2, "p"));
			Assert.AreEqual(NullValueStatus.DefinitelyNotNull, analysis.GetVariableStatusBeforeStatement(stmt3, "p"));
		}

		[Test]
		public void TestEndlessLoop()
		{
			var method = new MethodDeclaration {
				Body = new BlockStatement {
					new VariableDeclarationStatement(new PrimitiveType("string"),
					                                 "p2", new NullReferenceExpression()),
					new WhileStatement {
						Condition = new BinaryOperatorExpression(new IdentifierExpression("p1"),
						                                 BinaryOperatorType.Equality,
						                                 new NullReferenceExpression()),
						EmbeddedStatement = new ExpressionStatement(
							new AssignmentExpression(new IdentifierExpression("p2"),
						                         AssignmentOperatorType.Assign,
						                         new PrimitiveExpression("")))
					},
					new ReturnStatement()
				}
			};
			method.Parameters.Add(CreateStringParameter("p1"));

			var analysis = CreateNullValueAnalysis(method);
			var stmt1 = (WhileStatement)method.Body.Statements.ElementAt(1);
			var stmt2 = (ExpressionStatement)stmt1.EmbeddedStatement;
			var stmt3 = (ReturnStatement)method.Body.Statements.ElementAt(2);

			Assert.AreEqual(NullValueStatus.PotentiallyNull, analysis.GetVariableStatusBeforeStatement(stmt1, "p1"));
			Assert.AreEqual(NullValueStatus.DefinitelyNull, analysis.GetVariableStatusBeforeStatement(stmt1, "p2"));
			Assert.AreEqual(NullValueStatus.DefinitelyNull, analysis.GetVariableStatusBeforeStatement(stmt2, "p1"));
			Assert.AreEqual(NullValueStatus.PotentiallyNull, analysis.GetVariableStatusBeforeStatement(stmt2, "p2"));
			Assert.AreEqual(NullValueStatus.DefinitelyNull, analysis.GetVariableStatusAfterStatement(stmt2, "p1"));
			Assert.AreEqual(NullValueStatus.DefinitelyNotNull, analysis.GetVariableStatusAfterStatement(stmt2, "p2"));
			Assert.AreEqual(NullValueStatus.DefinitelyNotNull, analysis.GetVariableStatusBeforeStatement(stmt3, "p1"));
			Assert.AreEqual(NullValueStatus.DefinitelyNull, analysis.GetVariableStatusBeforeStatement(stmt3, "p2"));
		}

		[Test]
		public void TestLoop()
		{
			var method = new MethodDeclaration {
				Body = new BlockStatement {
					new VariableDeclarationStatement(new PrimitiveType("string"),
					                                 "p2", new NullReferenceExpression()),
					new WhileStatement {
						Condition = new BinaryOperatorExpression(new IdentifierExpression("p1"),
						                                         BinaryOperatorType.Equality,
						                                         new NullReferenceExpression()),
						EmbeddedStatement = new BlockStatement {
							new ExpressionStatement(
								new AssignmentExpression(new IdentifierExpression("p2"),
						                         AssignmentOperatorType.Assign,
						                         new PrimitiveExpression(""))),
							new ExpressionStatement(
								new AssignmentExpression(new IdentifierExpression("p1"),
							                         AssignmentOperatorType.Assign,
							                         new PrimitiveExpression("")))
						}
					},
					new ReturnStatement()
				}
			};
			method.Parameters.Add(CreateStringParameter("p1"));

			var analysis = CreateNullValueAnalysis(method);
			var stmt1 = (WhileStatement)method.Body.Statements.ElementAt(1);
			var stmt2 = (ExpressionStatement)((BlockStatement)stmt1.EmbeddedStatement).Statements.Last();
			var stmt3 = (ReturnStatement)method.Body.Statements.ElementAt(2);

			Assert.AreEqual(NullValueStatus.PotentiallyNull, analysis.GetVariableStatusBeforeStatement(stmt1, "p1"));
			Assert.AreEqual(NullValueStatus.DefinitelyNull, analysis.GetVariableStatusBeforeStatement(stmt1, "p2"));
			Assert.AreEqual(NullValueStatus.DefinitelyNull, analysis.GetVariableStatusBeforeStatement(stmt2, "p1"));
			Assert.AreEqual(NullValueStatus.DefinitelyNotNull, analysis.GetVariableStatusBeforeStatement(stmt2, "p2"));
			Assert.AreEqual(NullValueStatus.DefinitelyNotNull, analysis.GetVariableStatusAfterStatement(stmt2, "p1"));
			Assert.AreEqual(NullValueStatus.DefinitelyNotNull, analysis.GetVariableStatusAfterStatement(stmt2, "p2"));
			Assert.AreEqual(NullValueStatus.DefinitelyNotNull, analysis.GetVariableStatusBeforeStatement(stmt3, "p1"));
			Assert.AreEqual(NullValueStatus.PotentiallyNull, analysis.GetVariableStatusBeforeStatement(stmt3, "p2"));
		}

		ExpressionStatement MakeStatement(Expression expr)
		{
			return new ExpressionStatement(expr);
		}

		[Test]
		public void TestForLoop()
		{
			var forStatement = new ForStatement();
			forStatement.Initializers.Add(MakeStatement(new AssignmentExpression(new IdentifierExpression("p2"),
			                                                                     new PrimitiveExpression(""))));
			forStatement.Condition = new BinaryOperatorExpression(new IdentifierExpression("p1"),
			                                                      BinaryOperatorType.Equality,
			                                                      new NullReferenceExpression());
			forStatement.Iterators.Add(MakeStatement(new AssignmentExpression(new IdentifierExpression("p2"),
			                                                                  AssignmentOperatorType.Assign,
			                                                                  new NullReferenceExpression())));
			forStatement.EmbeddedStatement = MakeStatement(new AssignmentExpression(new IdentifierExpression("p1"),
			                                                                        AssignmentOperatorType.Assign,
			                                                                        new PrimitiveExpression("")));
			var method = new MethodDeclaration {
				Body = new BlockStatement {
					forStatement,
					new ReturnStatement()
				}
			};
			method.Parameters.Add(CreateStringParameter("p1"));
			method.Parameters.Add(CreateStringParameter("p2"));

			var returnStatement = (ReturnStatement)method.Body.Statements.Last();
			var content = forStatement.EmbeddedStatement;

			var analysis = CreateNullValueAnalysis(method);

			Assert.AreEqual(NullValueStatus.PotentiallyNull, analysis.GetVariableStatusBeforeStatement(forStatement, "p1"));
			Assert.AreEqual(NullValueStatus.PotentiallyNull, analysis.GetVariableStatusBeforeStatement(forStatement, "p2"));
			Assert.AreEqual(NullValueStatus.DefinitelyNull, analysis.GetVariableStatusBeforeStatement(content, "p1"));
			Assert.AreEqual(NullValueStatus.DefinitelyNotNull, analysis.GetVariableStatusBeforeStatement(content, "p2"));
			Assert.AreEqual(NullValueStatus.DefinitelyNotNull, analysis.GetVariableStatusAfterStatement(content, "p2"));
			Assert.AreEqual(NullValueStatus.DefinitelyNotNull, analysis.GetVariableStatusBeforeStatement(returnStatement, "p1"));
			Assert.AreEqual(NullValueStatus.PotentiallyNull, analysis.GetVariableStatusBeforeStatement(returnStatement, "p2"));
		}

		[Test]
		public void TestNullCoallescing()
		{
			var method = new MethodDeclaration {
				Body = new BlockStatement {
					new ExpressionStatement(new AssignmentExpression(new IdentifierExpression("p1"),
					                                                 new BinaryOperatorExpression(new IdentifierExpression("p1"),
					                             BinaryOperatorType.NullCoalescing,
					                             new PrimitiveExpression(""))))
				}
			};

			method.Parameters.Add(CreateStringParameter("p1"));

			var analysis = CreateNullValueAnalysis(method);
			var stmt = method.Body.Statements.Single();

			Assert.AreEqual(NullValueStatus.DefinitelyNotNull, analysis.GetVariableStatusAfterStatement(stmt, "p1"));
		}

		[Test]
		public void TestCapturedLambdaVariables()
		{
			var method = new MethodDeclaration {
				Body = new BlockStatement {
					new VariableDeclarationStatement(AstType.Create("System.Action"),
					                                 "action",
					                                 new LambdaExpression {
						Body = new BlockStatement {
							MakeStatement(new AssignmentExpression(new IdentifierExpression("p1"),
							                                       new NullReferenceExpression()))
						}
					}),
					new ExpressionStatement(new InvocationExpression(new IdentifierExpression("action")))
				}
			};

			method.Parameters.Add(CreateStringParameter("p1"));
			method.Parameters.Add(CreateStringParameter("p2"));

			var analysis = CreateNullValueAnalysis(method);
			var declareLambda = (VariableDeclarationStatement)method.Body.Statements.First();
			var callLambda = (ExpressionStatement)method.Body.Statements.Last();

			Assert.AreEqual(NullValueStatus.PotentiallyNull, analysis.GetVariableStatusBeforeStatement(declareLambda, "p1"));
			Assert.AreEqual(NullValueStatus.PotentiallyNull, analysis.GetVariableStatusBeforeStatement(declareLambda, "p2"));
			Assert.AreEqual(NullValueStatus.CapturedUnknown, analysis.GetVariableStatusBeforeStatement(callLambda, "p1"));
			Assert.AreEqual(NullValueStatus.PotentiallyNull, analysis.GetVariableStatusBeforeStatement(callLambda, "p2"));
		}

		[Test]
		public void TestInvocation()
		{
			var parser = new CSharpParser();
			var tree = parser.Parse(@"
delegate void MyDelegate(string p1, out string p2);
class TestClass
{
	void TestMethod()
	{
		string p1 = null;
		string p2 = null;
		MyDelegate del = (string a, out string b) => { b = a; };
		del(p1 = """", out p2);
	}
}
", "test.cs");
			Assert.AreEqual(0, tree.Errors.Count);

			var method = tree.Descendants.OfType<MethodDeclaration>().Single();
			var analysis = CreateNullValueAnalysis(tree, method);

			var lastStatement = (ExpressionStatement)method.Body.Statements.Last();

			Assert.AreEqual(NullValueStatus.DefinitelyNotNull, analysis.GetVariableStatusAfterStatement(lastStatement, "p1"));
			Assert.AreEqual(NullValueStatus.CapturedUnknown, analysis.GetVariableStatusAfterStatement(lastStatement, "p2"));
		}

		[Test]
		public void TestCompileConstants()
		{
			var parser = new CSharpParser();
			var tree = parser.Parse(@"
class TestClass
{
	const int? value1 = null;
	const bool value2 = true;
	void TestMethod()
	{
		int? p1 = value2 ? value1 : 0;
	}
}
", "test.cs");
			Assert.AreEqual(0, tree.Errors.Count);

			var method = tree.Descendants.OfType<MethodDeclaration>().Single();
			var analysis = CreateNullValueAnalysis(tree, method);

			var lastStatement = (VariableDeclarationStatement)method.Body.Statements.Last();

			Assert.AreEqual(NullValueStatus.DefinitelyNull, analysis.GetVariableStatusAfterStatement(lastStatement, "p1"));
		}

		[Test]
		public void TestConditionalAnd()
		{
			var method = new MethodDeclaration {
				Body = new BlockStatement {
					new IfElseStatement {
						Condition = new BinaryOperatorExpression(
								new BinaryOperatorExpression(new IdentifierExpression("p1"),
						                                         BinaryOperatorType.Equality,
						                                         new NullReferenceExpression()),
								BinaryOperatorType.ConditionalAnd,
								new BinaryOperatorExpression(new IdentifierExpression("p2"),
						                                     BinaryOperatorType.Equality,
						                                     new NullReferenceExpression())),
						TrueStatement = new ExpressionStatement(new AssignmentExpression(
							new IdentifierExpression("p1"),
							new PrimitiveExpression("Hello")))
					},
					new ReturnStatement()
				}
			};
			method.Parameters.Add(CreateStringParameter("p1"));
			method.Parameters.Add(CreateStringParameter("p2"));

			var analysis = CreateNullValueAnalysis(method);
			var stmt1 = (IfElseStatement)method.Body.Statements.First();
			var stmt2 = (ExpressionStatement)stmt1.TrueStatement;
			var stmt3 = (ReturnStatement)method.Body.Statements.ElementAt(1);

			Assert.AreEqual(NullValueStatus.PotentiallyNull, analysis.GetVariableStatusBeforeStatement(stmt1, "p1"));
			Assert.AreEqual(NullValueStatus.PotentiallyNull, analysis.GetVariableStatusBeforeStatement(stmt1, "p2"));
			Assert.AreEqual(NullValueStatus.DefinitelyNull, analysis.GetVariableStatusBeforeStatement(stmt2, "p1"));
			Assert.AreEqual(NullValueStatus.DefinitelyNull, analysis.GetVariableStatusBeforeStatement(stmt2, "p2"));
			Assert.AreEqual(NullValueStatus.DefinitelyNotNull, analysis.GetVariableStatusAfterStatement(stmt2, "p1"));
			Assert.AreEqual(NullValueStatus.PotentiallyNull, analysis.GetVariableStatusBeforeStatement(stmt3, "p2"));
		}

		[Test]
		public void TestConditionalOr()
		{
			var method = new MethodDeclaration {
				Body = new BlockStatement {
					new IfElseStatement {
						Condition = new UnaryOperatorExpression(UnaryOperatorType.Not,
						                                        new BinaryOperatorExpression(
							new BinaryOperatorExpression(new IdentifierExpression("p1"),
						                             BinaryOperatorType.Equality,
						                             new NullReferenceExpression()),
							BinaryOperatorType.ConditionalOr,
							new BinaryOperatorExpression(new IdentifierExpression("p2"),
						                             BinaryOperatorType.Equality,
						                             new NullReferenceExpression()))),
						TrueStatement = new ExpressionStatement(new AssignmentExpression(
							new IdentifierExpression("p1"),
							new NullReferenceExpression()))
					},
					new ReturnStatement()
				}
			};
			method.Parameters.Add(CreateStringParameter("p1"));
			method.Parameters.Add(CreateStringParameter("p2"));

			var analysis = CreateNullValueAnalysis(method);
			var stmt1 = (IfElseStatement)method.Body.Statements.First();
			var stmt2 = (ExpressionStatement)stmt1.TrueStatement;
			var stmt3 = (ReturnStatement)method.Body.Statements.ElementAt(1);

			Assert.AreEqual(NullValueStatus.PotentiallyNull, analysis.GetVariableStatusBeforeStatement(stmt1, "p1"));
			Assert.AreEqual(NullValueStatus.PotentiallyNull, analysis.GetVariableStatusBeforeStatement(stmt1, "p2"));
			Assert.AreEqual(NullValueStatus.DefinitelyNotNull, analysis.GetVariableStatusBeforeStatement(stmt2, "p1"));
			Assert.AreEqual(NullValueStatus.DefinitelyNotNull, analysis.GetVariableStatusBeforeStatement(stmt2, "p2"));
			Assert.AreEqual(NullValueStatus.DefinitelyNull, analysis.GetVariableStatusAfterStatement(stmt2, "p1"));
			Assert.AreEqual(NullValueStatus.PotentiallyNull, analysis.GetVariableStatusBeforeStatement(stmt3, "p2"));
		}
	}
}

