// -----------------------------------------------------------------------------
// FILE:	    UnitTest1.cs
// CONTRIBUTOR: NEONFORGE Team
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License").
// You may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Linq;

using FluentAssertions;

using Microsoft.Build.Utilities;

using Neon.Operator.Tasks;

namespace Test.Tasks
{
    public class Test_CustomResourceGenerator
    {
        [Fact]
        public void GenerateCustomResourcesFromDll()
        {
            // Arrange
            var taskInstance = new GenerateCustomResourceDefinitions()
            {
                InputDlls = new[]
                {
                    new TaskItem($@"..\..\..\..\test-operator\bin\Debug\net7.0\test-operator.dll")
                }
            };

            // Act
            var result = taskInstance.Execute();

            // Assert
            result.Should().BeTrue();

            taskInstance.CustomResourceDefinitions.Select(taskItem
                => taskItem.GetMetadata(GenerateCustomResourceDefinitions.NameKey)).Should().ContainSingle();


        }
    }
}