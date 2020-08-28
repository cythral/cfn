﻿using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Amazon.Lambda.ApplicationLoadBalancerEvents;
using Amazon.S3;

using Cythral.CloudFormation.AwsUtils;
using Cythral.CloudFormation.AwsUtils.CloudFormation;
using Cythral.CloudFormation.GithubWebhook;
using Cythral.CloudFormation.GithubWebhook.Entities;
using Cythral.CloudFormation.StackDeployment;

using NSubstitute;

using NUnit.Framework;

using RichardSzalay.MockHttp;

using static System.Text.Json.JsonSerializer;

using Handler = Cythral.CloudFormation.GithubWebhook.Handler;

namespace Cythral.CloudFormation.Tests.GithubWebhook
{
    // public class HandlerTests
    // {
    //     private static RequestValidator requestValidator = Substitute.For<RequestValidator>();
    //     private static DeployStackFacade stackDeployer = Substitute.For<DeployStackFacade>();
    //     private static PipelineStarter pipelineStarter = Substitute.For<PipelineStarter>();
    //     private static AmazonClientFactory<IAmazonS3> s3Factory = Substitute.For<AmazonClientFactory<IAmazonS3>>();
    //     private static IAmazonS3 s3Client = Substitute.For<IAmazonS3>();

    //     private const string repoName = "repoName";

    //     [SetUp]
    //     public void SetupStackDeployer()
    //     {
    //         TestUtils.SetPrivateStaticField(typeof(Handler), "stackDeployer", stackDeployer);
    //         stackDeployer.ClearReceivedCalls();
    //     }

    //     [SetUp]
    //     public void SetupPipelineStarter()
    //     {
    //         TestUtils.SetPrivateStaticField(typeof(Handler), "pipelineStarter", pipelineStarter);
    //         pipelineStarter.ClearReceivedCalls();
    //         pipelineStarter.StartPipelineIfExists(Arg.Any<PushEvent>()).Returns(Task.Run(() => { }));
    //     }

    //     [SetUp]
    //     public void SetupRequestValidator()
    //     {
    //         TestUtils.SetPrivateStaticField(typeof(Handler), "requestValidator", requestValidator);
    //         requestValidator.ClearReceivedCalls();
    //     }

    //     [SetUp]
    //     public void SetupS3()
    //     {
    //         TestUtils.SetPrivateStaticField(typeof(Handler), "s3Factory", s3Factory);
    //         s3Factory.ClearReceivedCalls();
    //         s3Factory.Create().Returns(s3Client);
    //     }

    //     [SetUp]
    //     public void SetupConfig()
    //     {
    //         Handler.Config = new Config()
    //         {
    //             ["GITHUB_TOKEN"] = "exampletoken",
    //             ["GITHUB_OWNER"] = "Codertocat",
    //             ["TEMPLATE_FILENAME"] = "cicd.template.yml",
    //             ["CONFIG_FILENAME"] = "cicd.config.yml",
    //             ["STACK_SUFFIX"] = "cicd",
    //             ["GITHUB_SIGNING_SECRET"] = "",
    //             ["ROLE_ARN"] = "arn:aws:iam::1:role/Facade",
    //             ["PIPELINE_DEFINITION_FILENAME"] = "pipeline.json",
    //         };
    //     }

    //     private (ApplicationLoadBalancerRequest, PushEvent) CreateRequest(string contentsUrl, string toRef, string defaultBranch, string head = null, string headCommitMessage = "")
    //     {
    //         var payload = new PushEvent
    //         {
    //             Ref = toRef,
    //             Head = head,
    //             HeadCommit = new Commit
    //             {
    //                 Message = headCommitMessage,
    //             },
    //             Repository = new Repository
    //             {
    //                 Name = repoName,
    //                 Owner = new User { Name = "Codertocat" },
    //                 ContentsUrl = contentsUrl,
    //                 DefaultBranch = defaultBranch,
    //             }
    //         };

    //         requestValidator.Validate(null, null, true, null).ReturnsForAnyArgs(payload);

    //         return (new ApplicationLoadBalancerRequest
    //         {
    //             Body = Serialize(payload)
    //         }, payload);
    //     }

    //     [Test]
    //     public async Task HandleCallsDeployIfOnDefaultBranch()
    //     {
    //         var contentsUrl = "https://api.github.com/repos/Codertocat/Hello-World/contents/{+path}";
    //         var (request, payload) = CreateRequest(contentsUrl, "refs/heads/master", "master");
    //         var template = "template";

    //         Console.WriteLine(payload.OnDefaultBranch);

    //         var httpMock = new MockHttpMessageHandler();
    //         CommittedFile.DefaultHttpClientFactory = () => new HttpClient(httpMock);

    //         httpMock
    //        .Expect($"https://api.github.com/repos/Codertocat/Hello-World/contents/cicd.template.yml")
    //        .Respond(HttpStatusCode.OK, "text/plain", template);

    //         var response = await Handler.Handle(request);
    //         await stackDeployer.Received().Deploy(Arg.Is<DeployStackContext>(req => req.Template == template));
    //     }

    //     [Test]
    //     public async Task HandleDoesNotCallDeployIfCommitMessageIncludesSkipCiMeta()
    //     {
    //         var contentsUrl = "https://api.github.com/repos/Codertocat/Hello-World/contents/{+path}";
    //         var (request, _) = CreateRequest(contentsUrl, "refs/heads/master", "master", headCommitMessage: "update docs [skip meta-ci]");
    //         var template = "template";

    //         var httpMock = new MockHttpMessageHandler();
    //         CommittedFile.DefaultHttpClientFactory = () => new HttpClient(httpMock);

    //         httpMock
    //        .Expect($"https://api.github.com/repos/Codertocat/Hello-World/contents/cicd.template.yml")
    //        .Respond(HttpStatusCode.OK, "text/plain", template);

    //         var response = await Handler.Handle(request);
    //         await stackDeployer.DidNotReceive().Deploy(Arg.Any<DeployStackContext>());
    //     }

    //     [Test]
    //     public async Task HandleDoesNotCallDeployIfNotOnDefaultBranch()
    //     {
    //         var contentsUrl = "https://api.github.com/repos/Codertocat/Hello-World/contents/{+path}";
    //         var (request, _) = CreateRequest(contentsUrl, "refs/heads/some-hotfix", "master");
    //         var template = "template";

    //         var httpMock = new MockHttpMessageHandler();
    //         CommittedFile.DefaultHttpClientFactory = () => new HttpClient(httpMock);

    //         httpMock
    //        .Expect($"https://api.github.com/repos/Codertocat/Hello-World/contents/cicd.template.yml")
    //        .Respond(HttpStatusCode.OK, "text/plain", template);

    //         var response = await Handler.Handle(request);
    //         await stackDeployer.DidNotReceive().Deploy(Arg.Any<DeployStackContext>());
    //     }

    //     [Test]
    //     public async Task HandleCallsStartPipeline()
    //     {
    //         var head = "head";
    //         var pushRef = "refs/heads/some-hotfix";
    //         var contentsUrl = "https://api.github.com/repos/Codertocat/Hello-World/contents/{+path}";
    //         var (request, payload) = CreateRequest(contentsUrl, pushRef, "master", head);
    //         var template = "template";

    //         var httpMock = new MockHttpMessageHandler();
    //         CommittedFile.DefaultHttpClientFactory = () => new HttpClient(httpMock);

    //         httpMock
    //        .Expect($"https://api.github.com/repos/Codertocat/Hello-World/contents/cicd.template.yml")
    //        .Respond(HttpStatusCode.OK, "text/plain", template);

    //         var response = await Handler.Handle(request);
    //         await pipelineStarter.Received().StartPipelineIfExists(Arg.Is(payload));
    //     }

    //     [Test]
    //     public async Task HandleDoesntCallStartPipelineIfHeadCommitMessageContainsSkipCI()
    //     {
    //         var head = "head";
    //         var pushRef = "refs/heads/some-hotfix";
    //         var contentsUrl = "https://api.github.com/repos/Codertocat/Hello-World/contents/{+path}";
    //         var (request, payload) = CreateRequest(contentsUrl, pushRef, "master", head, "yada yada [skip ci]");
    //         var template = "template";

    //         var httpMock = new MockHttpMessageHandler();
    //         CommittedFile.DefaultHttpClientFactory = () => new HttpClient(httpMock);

    //         httpMock
    //        .Expect($"https://api.github.com/repos/Codertocat/Hello-World/contents/cicd.template.yml")
    //        .Respond(HttpStatusCode.OK, "text/plain", template);

    //         var response = await Handler.Handle(request);
    //         await pipelineStarter.DidNotReceive().StartPipelineIfExists(Arg.Any<PushEvent>());
    //     }
    // }
}