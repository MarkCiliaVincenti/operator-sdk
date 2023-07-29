using System;

using FluentAssertions;

using Neon.Operator.Analyzers;

using Xunit.Abstractions;

namespace Test.Analyzers
{
    public class Test_MutatingWebhooks
    {
        private readonly ITestOutputHelper output;
        public Test_MutatingWebhooks(ITestOutputHelper output)
        {
            this.output = output ?? throw new ArgumentNullException(nameof(output));
        }

        [Fact]
        public void TestCreateMutatingWebhookControllerFromInterface()
        {
            var mutatingWebhook = @"
using k8s.Models;
using Neon.Operator.Webhooks;
using System.Threading.Tasks;

namespace TestOperator
{
    public class PodWebhook : IMutatingWebhook<V1Pod>
    {
        private bool modified = false;

        public async Task<MutationResult> CreateAsync(V1Pod entity, bool dryRun)
        {
            if (modified)
            {
                return await Task.FromResult(MutationResult.Modified(entity));
            }

            return await Task.FromResult(MutationResult.NoChanges());
        }

        public async Task<MutationResult> UpdateAsync(V1Pod entity, V1Pod oldEntity, bool dryRun)
        {
            if (modified)
            {
                return await Task.FromResult(MutationResult.Modified(entity));
            }

            return await Task.FromResult(MutationResult.NoChanges());
        }
    }
}";
            var expectedController = @"//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by the NeonFORGE Operator SDK.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Threading.Tasks;

using k8s;
using k8s.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Neon.Diagnostics;
using Neon.Operator;
using Neon.Operator.Webhooks;

using Prometheus;

namespace TestOperator.Controllers
{
    [ApiController]
    public class PodWebhookController : ControllerBase
    {
        private WebhookMetrics<V1Pod> metrics;
        private IAdmissionWebhook<V1Pod, MutationResult> webhook;
        private OperatorSettings operatorSettings;
        private ILogger<PodWebhookController> logger;

        public PodWebhookController(
            IAdmissionWebhook<V1Pod, MutationResult> webhook,
            WebhookMetrics<V1Pod> metrics,
            OperatorSettings operatorSettings,
            ILogger<PodWebhookController> logger = null)
        {
            this.webhook = webhook;
            this.metrics = metrics;
            this.operatorSettings = operatorSettings;
            this.logger = logger;
        }


        [HttpPost(""v1/pods/podwebhook/mutate"")]
        public async Task<ActionResult<MutationResult>> V1PodWebhookAsync([FromBody] AdmissionReview<V1Pod> admissionRequest)
        {
            using var activity = Activity.Current;
            using var inFlight = metrics.RequestsInFlight.TrackInProgress();
            using var timer    = metrics.LatencySeconds.NewTimer();

            AdmissionResponse response;

            try
            {

                var @object   = KubernetesJson.Deserialize<V1Pod>(KubernetesJson.Serialize(admissionRequest.Request.Object));
                var oldObject = KubernetesJson.Deserialize<V1Pod>(KubernetesJson.Serialize(admissionRequest.Request.OldObject));

                logger?.LogInformationEx(() => @$""Admission with method """"{admissionRequest.Request.Operation}""""."");

                MutationResult result;

                switch (admissionRequest.Request.Operation)
                {
                    case ""CREATE"":

                        result = await webhook.CreateAsync(@object, admissionRequest.Request.DryRun);

                        break;

                    case ""UPDATE"":

                        result = await webhook.UpdateAsync(oldObject, @object, admissionRequest.Request.DryRun);
                        break;

                    case ""DELETE"":

                        result = await webhook.DeleteAsync(@object, admissionRequest.Request.DryRun);
                        break;

                    default:
                        throw new InvalidOperationException();
                }

                response = webhook.TransformResult(result, admissionRequest.Request);

            }
            catch (Exception e)
            {
                logger?.LogErrorEx(e, ""An error happened during admission."");

                response = new AdmissionResponse()
                {
                    Allowed = false,
                    Status = new()
                    {
                        Code = StatusCodes.Status500InternalServerError,
                        Message = ""There was an internal server error."",
                    },
                };
            }

            admissionRequest.Response = response;
            admissionRequest.Response.Uid = admissionRequest.Request.Uid;

            logger?.LogInformationEx(() => @$""AdmissionHook """"{webhook.Name}"""" did return """"{admissionRequest.Response?.Allowed}"""" for """"{admissionRequest.Request.Operation}""""."");
            admissionRequest.Request = null;

            metrics.RequestsTotal.WithLabels(new string[] { operatorSettings.Name, webhook.Endpoint, response.Status?.Code.ToString() }).Inc();

            return Ok(admissionRequest);
        }
    }
}";

            var generatedCode = CompilationHelper.GetGeneratedOutput<MutatingWebhookGenerator>(mutatingWebhook);
            generatedCode.Should().NotBeNull();

            generatedCode.TrimEnd().Should().BeEquivalentTo(expectedController.TrimEnd());

        }

        [Fact]
        public void TestCreateMutatingWebhookControllerFromBaseClass()
        {
            var mutatingWebhook = @"
using k8s.Models;
using Neon.Operator.Webhooks;
using System.Threading.Tasks;

namespace TestOperator
{
    public class PodWebhook : MutatingWebhookBase<V1Pod>
    {
        private bool modified = false;

        public async Task<MutationResult> CreateAsync(V1Pod entity, bool dryRun)
        {
            if (modified)
            {
                return await Task.FromResult(MutationResult.Modified(entity));
            }

            return await Task.FromResult(MutationResult.NoChanges());
        }

        public async Task<MutationResult> UpdateAsync(V1Pod entity, V1Pod oldEntity, bool dryRun)
        {
            if (modified)
            {
                return await Task.FromResult(MutationResult.Modified(entity));
            }

            return await Task.FromResult(MutationResult.NoChanges());
        }
    }
}";
            var expectedController = @"//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by the NeonFORGE Operator SDK.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Threading.Tasks;

using k8s;
using k8s.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Neon.Diagnostics;
using Neon.Operator;
using Neon.Operator.Webhooks;

using Prometheus;

namespace TestOperator.Controllers
{
    [ApiController]
    public class PodWebhookController : ControllerBase
    {
        private WebhookMetrics<V1Pod> metrics;
        private IAdmissionWebhook<V1Pod, MutationResult> webhook;
        private OperatorSettings operatorSettings;
        private ILogger<PodWebhookController> logger;

        public PodWebhookController(
            IAdmissionWebhook<V1Pod, MutationResult> webhook,
            WebhookMetrics<V1Pod> metrics,
            OperatorSettings operatorSettings,
            ILogger<PodWebhookController> logger = null)
        {
            this.webhook = webhook;
            this.metrics = metrics;
            this.operatorSettings = operatorSettings;
            this.logger = logger;
        }


        [HttpPost(""v1/pods/podwebhook/mutate"")]
        public async Task<ActionResult<MutationResult>> V1PodWebhookAsync([FromBody] AdmissionReview<V1Pod> admissionRequest)
        {
            using var activity = Activity.Current;
            using var inFlight = metrics.RequestsInFlight.TrackInProgress();
            using var timer    = metrics.LatencySeconds.NewTimer();

            AdmissionResponse response;

            try
            {

                var @object   = KubernetesJson.Deserialize<V1Pod>(KubernetesJson.Serialize(admissionRequest.Request.Object));
                var oldObject = KubernetesJson.Deserialize<V1Pod>(KubernetesJson.Serialize(admissionRequest.Request.OldObject));

                logger?.LogInformationEx(() => @$""Admission with method """"{admissionRequest.Request.Operation}""""."");

                MutationResult result;

                switch (admissionRequest.Request.Operation)
                {
                    case ""CREATE"":

                        result = await webhook.CreateAsync(@object, admissionRequest.Request.DryRun);

                        break;

                    case ""UPDATE"":

                        result = await webhook.UpdateAsync(oldObject, @object, admissionRequest.Request.DryRun);
                        break;

                    case ""DELETE"":

                        result = await webhook.DeleteAsync(@object, admissionRequest.Request.DryRun);
                        break;

                    default:
                        throw new InvalidOperationException();
                }

                response = webhook.TransformResult(result, admissionRequest.Request);

            }
            catch (Exception e)
            {
                logger?.LogErrorEx(e, ""An error happened during admission."");

                response = new AdmissionResponse()
                {
                    Allowed = false,
                    Status = new()
                    {
                        Code = StatusCodes.Status500InternalServerError,
                        Message = ""There was an internal server error."",
                    },
                };
            }

            admissionRequest.Response = response;
            admissionRequest.Response.Uid = admissionRequest.Request.Uid;

            logger?.LogInformationEx(() => @$""AdmissionHook """"{webhook.Name}"""" did return """"{admissionRequest.Response?.Allowed}"""" for """"{admissionRequest.Request.Operation}""""."");
            admissionRequest.Request = null;

            metrics.RequestsTotal.WithLabels(new string[] { operatorSettings.Name, webhook.Endpoint, response.Status?.Code.ToString() }).Inc();

            return Ok(admissionRequest);
        }
    }
}";

            var generatedCode = CompilationHelper.GetGeneratedOutput<MutatingWebhookGenerator>(mutatingWebhook);
            generatedCode.Should().NotBeNull();

            generatedCode.TrimEnd().Should().BeEquivalentTo(expectedController.TrimEnd());

        }
    }
}