// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if CoreCLR

using System;
using System.Globalization;
using System.Reflection;
using Microsoft.PowerShell.EditorServices.Hosting;
using Xunit;

namespace PowerShellEditorServices.Test.Session
{
    [Trait("Category", "PsesLoadContext")]
    public class PsesLoadContextTests
    {
        // Two distinct, realistic public key tokens: Newtonsoft.Json's and the ECMA/Microsoft one.
        private static readonly byte[] s_tokenA = { 0x30, 0xad, 0x4f, 0xe6, 0xb2, 0xa6, 0xae, 0xed };
        private static readonly byte[] s_tokenB = { 0xb0, 0x3f, 0x5f, 0x7f, 0x11, 0xd5, 0x0a, 0x3a };

        private static AssemblyName MakeName(
            string name,
            string version = "1.0.0.0",
            byte[] publicKeyToken = null,
            string culture = "")
        {
            AssemblyName assemblyName = new(name)
            {
                Version = new Version(version),
                CultureInfo = string.IsNullOrEmpty(culture)
                    ? CultureInfo.InvariantCulture
                    : new CultureInfo(culture),
            };

            assemblyName.SetPublicKeyToken(publicKeyToken);
            return assemblyName;
        }

        [Fact]
        public void IsSatisfyingWhenIdentityMatchesExactly()
        {
            AssemblyName required = MakeName("Contoso.Lib", "2.0.0.0", s_tokenA);
            AssemblyName candidate = MakeName("Contoso.Lib", "2.0.0.0", s_tokenA);

            Assert.True(PsesLoadContext.IsSatisfyingAssembly(required, candidate));
        }

        [Fact]
        public void IsSatisfyingWhenCandidateVersionIsNewer()
        {
            AssemblyName required = MakeName("Contoso.Lib", "1.0.0.0", s_tokenA);
            AssemblyName candidate = MakeName("Contoso.Lib", "2.5.0.0", s_tokenA);

            Assert.True(PsesLoadContext.IsSatisfyingAssembly(required, candidate));
        }

        [Fact]
        public void IsNotSatisfyingWhenCandidateVersionIsOlder()
        {
            AssemblyName required = MakeName("Contoso.Lib", "2.0.0.0", s_tokenA);
            AssemblyName candidate = MakeName("Contoso.Lib", "1.0.0.0", s_tokenA);

            Assert.False(PsesLoadContext.IsSatisfyingAssembly(required, candidate));
        }

        [Fact]
        public void IsNotSatisfyingWhenSimpleNameDiffers()
        {
            AssemblyName required = MakeName("Contoso.Lib", "1.0.0.0", s_tokenA);
            AssemblyName candidate = MakeName("Fabrikam.Lib", "1.0.0.0", s_tokenA);

            Assert.False(PsesLoadContext.IsSatisfyingAssembly(required, candidate));
        }

        [Fact]
        public void IsSatisfyingWhenSimpleNameDiffersOnlyByCase()
        {
            AssemblyName required = MakeName("Contoso.Lib", "1.0.0.0", s_tokenA);
            AssemblyName candidate = MakeName("contoso.lib", "1.0.0.0", s_tokenA);

            Assert.True(PsesLoadContext.IsSatisfyingAssembly(required, candidate));
        }

        // This is the core fix: matching name and version but a different strong-name identity
        // must NOT be treated as a drop-in replacement, since binding to it would fail at runtime.
        [Fact]
        public void IsNotSatisfyingWhenPublicKeyTokenDiffers()
        {
            AssemblyName required = MakeName("Contoso.Lib", "1.0.0.0", s_tokenA);
            AssemblyName candidate = MakeName("Contoso.Lib", "1.0.0.0", s_tokenB);

            Assert.False(PsesLoadContext.IsSatisfyingAssembly(required, candidate));
        }

        [Fact]
        public void IsNotSatisfyingWhenRequiredIsStrongNamedButCandidateIsNot()
        {
            AssemblyName required = MakeName("Contoso.Lib", "1.0.0.0", s_tokenA);
            AssemblyName candidate = MakeName("Contoso.Lib", "1.0.0.0", publicKeyToken: null);

            Assert.False(PsesLoadContext.IsSatisfyingAssembly(required, candidate));
        }

        // A reference to a non-strong-named assembly imposes no public key token requirement.
        [Fact]
        public void IsSatisfyingWhenRequiredIsNotStrongNamedRegardlessOfCandidateToken()
        {
            AssemblyName required = MakeName("Contoso.Lib", "1.0.0.0", publicKeyToken: null);
            AssemblyName candidate = MakeName("Contoso.Lib", "1.0.0.0", s_tokenA);

            Assert.True(PsesLoadContext.IsSatisfyingAssembly(required, candidate));
        }

        [Fact]
        public void IsNotSatisfyingWhenCultureDiffers()
        {
            AssemblyName required = MakeName("Contoso.Lib", "1.0.0.0", s_tokenA, culture: "");
            AssemblyName candidate = MakeName("Contoso.Lib", "1.0.0.0", s_tokenA, culture: "fr");

            Assert.False(PsesLoadContext.IsSatisfyingAssembly(required, candidate));
        }

        [Fact]
        public void IsSatisfyingWhenCultureMatches()
        {
            AssemblyName required = MakeName("Contoso.Lib", "1.0.0.0", s_tokenA, culture: "fr");
            AssemblyName candidate = MakeName("Contoso.Lib", "1.0.0.0", s_tokenA, culture: "fr");

            Assert.True(PsesLoadContext.IsSatisfyingAssembly(required, candidate));
        }
    }
}

#endif
