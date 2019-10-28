using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Cythral.CloudFormation.BuildTasks.Converters;
using Cythral.CloudFormation.BuildTasks.Models;

using Task = System.Threading.Tasks.Task;

namespace Cythral.CloudFormation.BuildTasks {
    public class Deploy : Microsoft.Build.Utilities.Task {

        public class Config {
            public List<Parameter> Parameters { get; set; }
            public List<Tag> Tags { get; set; }
            public StackPolicyBody StackPolicy { get; set; }
        }

        [Required]
        public string StackName { get; set; }

        [Required]
        public string TemplateFile { get; set; }

        [Required]
        public string ConfigFile { get; set; }

        public string Capabilities { get; set; }

        public IAmazonCloudFormation Client { get; set; } = new AmazonCloudFormationClient();

        public override bool Execute() {
            Task.WaitAll(new Task[] { 
                Task.Run(async delegate {
                    var template = await GetTemplateFileContents();
                    var config = await GetConfigFileContents();
                    var describeStacksRequest = new DescribeStacksRequest { StackName = StackName };
                    var stackExists = (await Client.DescribeStacksAsync()).Stacks.Count() != 0;
                    string status;

                    try {
                        if(!stackExists) {
                            await Client.CreateStackAsync(new CreateStackRequest {
                                StackName = StackName,
                                Capabilities = Capabilities?.Split(",")?.ToList(),
                                TemplateBody = template,
                                StackPolicyBody = config.StackPolicy?.ToString(),
                                Parameters = config.Parameters,
                                Tags = config.Tags
                            });
                        } else {
                            await Client.UpdateStackAsync(new UpdateStackRequest {
                                StackName = StackName,
                                Capabilities = Capabilities?.Split(",")?.ToList(),
                                TemplateBody = template,
                                StackPolicyBody = config.StackPolicy?.ToString(),
                                Parameters = config.Parameters,
                                Tags = config.Tags
                            });
                        }
                    } catch(AmazonCloudFormationException e) {
                        if(e.Message == "No updates are to be performed.") {
                            Console.WriteLine("Done.");
                            return;
                        } else throw e;
                    }

                    Thread.Sleep(200);
                    
                    while(
                        (status = ((await Client.DescribeStacksAsync(describeStacksRequest)).Stacks[0].StackStatus.Value))
                        .EndsWith("_IN_PROGRESS")
                    ) {
                        Thread.Sleep(5000);
                        Console.WriteLine("Waiting for create/update to complete....");
                    }

                    
                    if(status.StartsWith("ROLLBACK") || status.EndsWith("FAILED")) {
                        throw new Exception("Deployment failed.  Check the stack logs.");        
                    }

                    Console.WriteLine("Done.");                    
                })
            });
            return true;
        }

        private async Task<Config> GetConfigFileContents() {
            if(!File.Exists(ConfigFile)) {
                throw new Exception($"{ConfigFile} does not exist.");
            }

            var stream = File.OpenRead(ConfigFile);
            var options = new JsonSerializerOptions();
            
            options.Converters.Add(new ParameterConverter());
            options.Converters.Add(new TagConverter());
            options.Converters.Add(new StackPolicyBodyConverter());

            return await JsonSerializer.DeserializeAsync<Config>(stream, options);
        }

        private async Task<string> GetTemplateFileContents() {
            return await File.ReadAllTextAsync(TemplateFile);
        }

        public static void Main(string[] args) {}
    }
}
