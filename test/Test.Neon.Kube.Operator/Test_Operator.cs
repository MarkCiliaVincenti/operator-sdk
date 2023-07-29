//-----------------------------------------------------------------------------
// FILE:	    TestOperator.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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

using System.Linq;
using System.Threading.Tasks;

using k8s.Models;

using Neon.Operator.Xunit;

using Xunit;

namespace Test.Neon.Operator
{
    public class TestOperator : IClassFixture<TestOperatorFixture>
    {
        private TestOperatorFixture fixture;

        public TestOperator(TestOperatorFixture fixture) 
        { 
            this.fixture = fixture;
            fixture.Operator.AddController<TestResourceController>();
            fixture.Operator.AddController<TestDatabaseController>();
            fixture.RegisterType<V1TestChildResource>();
            fixture.RegisterType<V1StatefulSet>();
            fixture.RegisterType<V1Service>();
            fixture.Start();
        }

        [Fact]
        public async Task CreateTestObjectAsync()
        {
            fixture.ClearResources();

            var controller = fixture.Operator.GetController<TestResourceController>();

            var resource = new V1TestResource();
            resource.Spec = new TestSpec()
            {
                Message = "I'm the parent object"
            };

            await controller.ReconcileAsync(resource);

            Assert.Contains(fixture.Resources, r => r.Metadata.Name == "child-object");

            Assert.Single(fixture.Resources);
        }

        [Fact]
        public async Task CreateStatefulSetAsync()
        {
            fixture.ClearResources();

            var controller = fixture.Operator.GetController<TestDatabaseController>();

            var resource = new V1TestDatabase()
            {
                Metadata = new V1ObjectMeta()
                {
                    Name = "test-database",
                    NamespaceProperty = "test"
                },
                Spec = new TestDatabaseSpec()
                {
                    Image = "foo/bar:latest",
                    Servers = 3,
                    VolumeSize = "1Gi"
                }
            };

            await controller.ReconcileAsync(resource);

            var statefulsets = fixture.Resources.OfType<V1StatefulSet>();
            var services = fixture.Resources.OfType<V1Service>();

            Assert.Equal(2, fixture.Resources.Count);

            // verify statefulset
            Assert.Contains(statefulsets, r => r.Metadata.Name == resource.Name());
            Assert.Equal(resource.Spec.Servers, statefulsets.Single().Spec.Replicas);

            // verify service
            Assert.Contains(services, r => r.Metadata.Name == resource.Name());
        }
    }
}