#region Copyright & License Information
/*
 * Copyright 2007-2017 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenRA.Support;

namespace OpenRA.Test
{
	[TestFixture]
	public class ConditionExpressionTest
	{
		IReadOnlyDictionary<string, int> testValues = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>()
		{
			{ "true", 1 },
			{ "false", 0 }
		});

		void AssertFalse(string expression)
		{
			Assert.False(new ConditionExpression(expression).Evaluate(testValues) > 0, expression);
		}

		void AssertTrue(string expression)
		{
			Assert.True(new ConditionExpression(expression).Evaluate(testValues) > 0, expression);
		}

		void AssertValue(string expression, int value)
		{
			Assert.AreEqual(value, new ConditionExpression(expression).Evaluate(testValues), expression);
		}

		void AssertParseFailure(string expression)
		{
			Assert.Throws(typeof(InvalidDataException), () => new ConditionExpression(expression).Evaluate(testValues), expression);
		}

		void AssertParseFailure(string expression, string errorMessage)
		{
			var actualErrorMessage = Assert.Throws(typeof(InvalidDataException),
			                                       () => new ConditionExpression(expression).Evaluate(testValues),
			                                       expression).Message;
			Assert.AreEqual(errorMessage, actualErrorMessage, expression + "   ===>   " + actualErrorMessage);
		}

		[TestCase(TestName = "Numbers")]
		public void TestNumbers()
		{
			AssertParseFailure("1a", "Number 1 and variable merged at index 0");
			AssertValue("0", 0);
			AssertValue("1", 1);
			AssertValue("12", 12);
			AssertValue("-1", -1);
			AssertValue("-12", -12);
		}

		[TestCase(TestName = "AND operation")]
		public void TestAnd()
		{
			AssertTrue("true && true");
			AssertFalse("false && false");
			AssertFalse("true && false");
			AssertFalse("false && true");
			AssertValue("2 && false", 0);
			AssertValue("false && 2", 0);
			AssertValue("3 && 2", 2);
			AssertValue("2 && 3", 3);
		}

		[TestCase(TestName = "OR operation")]
		public void TestOR()
		{
			AssertTrue("true || true");
			AssertFalse("false || false");
			AssertTrue("true || false");
			AssertTrue("false || true");
			AssertValue("2 || false", 2);
			AssertValue("false || 2", 2);
			AssertValue("3 || 2", 3);
			AssertValue("2 || 3", 2);
		}

		[TestCase(TestName = "Equals operation")]
		public void TestEquals()
		{
			AssertTrue("true == true");
			AssertTrue("false == false");
			AssertFalse("true == false");
			AssertFalse("false == true");
			AssertTrue("1 == 1");
			AssertTrue("0 == 0");
			AssertFalse("1 == 0");
			AssertTrue("1 == true");
			AssertFalse("1 == false");
			AssertTrue("0 == false");
			AssertFalse("0 == true");
			AssertValue("12 == 12", 1);
			AssertValue("1 == 12", 0);
		}

		[TestCase(TestName = "Not-equals operation")]
		public void TestNotEquals()
		{
			AssertFalse("true != true");
			AssertFalse("false != false");
			AssertTrue("true != false");
			AssertTrue("false != true");
			AssertValue("1 != 2", 1);
			AssertValue("1 != 1", 0);
			AssertFalse("1 != true");
			AssertFalse("0 != false");
			AssertTrue("1 != false");
			AssertTrue("0 != true");
		}

		[TestCase(TestName = "NOT operation")]
		public void TestNOT()
		{
			AssertValue("!true", 0);
			AssertValue("!false", 1);
			AssertValue("!!true", 1);
			AssertValue("!!false", 0);
			AssertValue("!0", 1);
			AssertValue("!1", 0);
			AssertValue("!5", 0);
			AssertValue("!!5", 1);
			AssertValue("!-5", 1);
		}

		[TestCase(TestName = "Precedence")]
		public void TestPrecedence()
		{
			AssertTrue("true && false || true");
			AssertFalse("false || false && true");
			AssertTrue("true && !true || !false");
			AssertFalse("false || !true && !false");
		}

		[TestCase(TestName = "Parenthesis")]
		public void TestParens()
		{
			AssertTrue("(true)");
			AssertTrue("((true))");
			AssertFalse("(false)");
			AssertFalse("((false))");
		}

		[TestCase(TestName = "Parenthesis and mixed operations")]
		public void TestMixedParens()
		{
			AssertTrue("(!false)");
			AssertTrue("!(false)");
			AssertFalse("!(!false)");
			AssertTrue("(true) || (false)");
			AssertTrue("true && (false || true)");
			AssertTrue("(true && false) || true");
			AssertTrue("!(true && false) || false");
			AssertTrue("((true != true) == false) && true");
			AssertFalse("(true != false) == false && true");
			AssertTrue("true || ((true != false) != !(false && true))");
			AssertFalse("((true != false) != !(false && true))");
		}

		[TestCase(TestName = "Test parser errors")]
		public void TestParseErrors()
		{
			AssertParseFailure("()", "Empty parenthesis at index 0");
			AssertParseFailure("! && true", "Missing value or sub-expression or there is an extra operator `!` at index 0 or `&&` at index 2");
			AssertParseFailure("(true", "Unclosed opening parenthesis at index 0");
			AssertParseFailure(")true", "Unmatched closing parenthesis at index 0");
			AssertParseFailure("false)", "Unmatched closing parenthesis at index 5");
			AssertParseFailure("false(", "Missing binary operation before `(` at index 5");
			AssertParseFailure("(", "Missing value or sub-expression at end for `(` operator");
			AssertParseFailure(")", "Unmatched closing parenthesis at index 0");
			AssertParseFailure("false!", "Missing binary operation before `!` at index 5");
			AssertParseFailure("true false", "Missing binary operation before `false` at index 5");
			AssertParseFailure("true & false", "Unexpected character '&' at index 5 - should it be `&&`?");
			AssertParseFailure("true | false", "Unexpected character '|' at index 5 - should it be `||`?");
			AssertParseFailure("true : false", "Invalid character ':' at index 5");
			AssertParseFailure("true & false && !", "Unexpected character '&' at index 5 - should it be `&&`?");
			AssertParseFailure("(true && !)", "Missing value or sub-expression or there is an extra operator `!` at index 9 or `)` at index 10");
			AssertParseFailure("&& false", "Missing value or sub-expression at beginning for `&&` operator");
			AssertParseFailure("false ||", "Missing value or sub-expression at end for `||` operator");
		}

		[TestCase(TestName = "Undefined symbols are treated as `false` (0) values")]
		public void TestUndefinedSymbols()
		{
			AssertFalse("undef1 || undef2");
		}
	}
}
