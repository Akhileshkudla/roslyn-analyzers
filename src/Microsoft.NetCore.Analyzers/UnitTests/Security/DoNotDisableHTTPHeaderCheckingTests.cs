﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Test.Utilities;
using Xunit;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class DoNotDisableHTTPHeaderCheckingTests : DiagnosticAnalyzerTestBase
    {
        [Fact]
        public void TestLiteralDiagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Web.Configuration;

class TestClass
{
    public void TestMethod()
    {
        var httpRuntimeSection = new HttpRuntimeSection();
        httpRuntimeSection.EnableHeaderChecking = false;
    }
}",
            GetCSharpResultAt(10, 9, DoNotDisableHTTPHeaderChecking.Rule));
        }

        [Fact]
        public void TestConstantDiagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Web.Configuration;

class TestClass
{
    public void TestMethod()
    {
        const bool flag = false;
        var httpRuntimeSection = new HttpRuntimeSection();
        httpRuntimeSection.EnableHeaderChecking = flag;
    }
}",
            GetCSharpResultAt(11, 9, DoNotDisableHTTPHeaderChecking.Rule));
        }

        [Fact]
        public void TestPropertyInitializerDiagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Web.Configuration;

class TestClass
{
    public void TestMethod()
    {
        var httpRuntimeSection = new HttpRuntimeSection
        {
            EnableHeaderChecking = false
        };
    }
}",
            GetCSharpResultAt(11, 13, DoNotDisableHTTPHeaderChecking.Rule));
        }

        //Ideally, we would generate a diagnostic in this case.
        [Fact]
        public void TestVariableNoDiagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Web.Configuration;

class TestClass
{
    public void TestMethod()
    {
        var flag = false;
        var httpRuntimeSection = new HttpRuntimeSection();
        httpRuntimeSection.EnableHeaderChecking = flag;
    }
}");
        }

        [Fact]
        public void TestLiteralNoDiagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Web.Configuration;

class TestClass
{
    public void TestMethod()
    {
        var httpRuntimeSection = new HttpRuntimeSection();
        httpRuntimeSection.EnableHeaderChecking = true;
    }
}");
        }

        [Fact]
        public void TestConstantNoDiagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Web.Configuration;

class TestClass
{
    public void TestMethod()
    {
        const bool flag = true;
        var httpRuntimeSection = new HttpRuntimeSection();
        httpRuntimeSection.EnableHeaderChecking = flag;
    }
}");
        }

        [Fact]
        public void TestPropertyInitializerNoDiagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Web.Configuration;

class TestClass
{
    public void TestMethod()
    {
        var httpRuntimeSection = new HttpRuntimeSection
        {
            EnableHeaderChecking = true
        };
    }
}");
        }

        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new DoNotDisableHTTPHeaderChecking();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new DoNotDisableHTTPHeaderChecking();
        }
    }
}
