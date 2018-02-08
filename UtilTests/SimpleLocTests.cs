﻿/// ---------------------------------------------------------------------------------
///
/// Copyright (c) 2018 Sebastian Hönel [sebastian.honel@lnu.se]
///
/// https://github.com/MrShoenel/git-density
///
/// This file is part of the project UtilTests. All files in this project,
/// if not noted otherwise, are licensed under the MIT-license.
///
/// ---------------------------------------------------------------------------------
///
/// Permission is hereby granted, free of charge, to any person obtaining a
/// copy of this software and associated documentation files (the "Software"),
/// to deal in the Software without restriction, including without limitation
/// the rights to use, copy, modify, merge, publish, distribute, sublicense,
/// and/or sell copies of the Software, and to permit persons to whom the
/// Software is furnished to do so, subject to the following conditions:
///
/// The above copyright notice and this permission notice shall be included in all
/// copies or substantial portions of the Software.
///
/// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
/// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
/// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
/// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
/// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
/// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
///
/// ---------------------------------------------------------------------------------
///
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Util.Extensions;

namespace UtilTests
{
	[TestClass]
	public class SimpleLocTests
	{
		public const string SingleLineComments =
			"var a = 1; // foo\n" +
			"// delete this\n" +
			"\t  //this, too"
			;

		[TestMethod]
		public void TestSingleLineComments()
		{
			var simpleLoc = new Util.Metrics.SimpleLoc(SingleLineComments.GetLines(removeEmptyLines: false));

			Assert.AreEqual(3u, simpleLoc.LocGross);
			Assert.AreEqual(1u, simpleLoc.LocNoComments);
		}
	}
}
