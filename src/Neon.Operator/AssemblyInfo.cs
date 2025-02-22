//-----------------------------------------------------------------------------
// FILE:	    AssemblyInfo.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Runtime.CompilerServices;

[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v7.0", FrameworkDisplayName = ".NET 7.0")]

[assembly: InternalsVisibleTo("Test.Neon.Kube")]
[assembly: InternalsVisibleTo("Test.Neon.Operator")]
[assembly: InternalsVisibleTo("Neon.Operator")]
[assembly: InternalsVisibleTo("Neon.Operator.XUnit")]
[assembly: InternalsVisibleTo("Neon.Kube.Xunit")]

