﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.AI.TextGeneration;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.ChatCompletion;
using Xunit;

namespace SemanticKernel.Connectors.UnitTests.OpenAI.ChatCompletion;

/// <summary>
/// Unit tests for <see cref="OpenAIChatCompletionService"/>
/// </summary>
public sealed class OpenAIChatCompletionTests : IDisposable
{
    private readonly HttpMessageHandlerStub _messageHandlerStub;
    private readonly HttpClient _httpClient;
    private readonly OpenAIFunction _timepluginDate, _timepluginNow;
    private readonly OpenAIPromptExecutionSettings _executionSettings;

    public OpenAIChatCompletionTests()
    {
        this._messageHandlerStub = new HttpMessageHandlerStub();
        this._httpClient = new HttpClient(this._messageHandlerStub, false);
        this._timepluginDate = new()
        {
            FunctionName = "Date",
            PluginName = "TimePlugin",
            Description = "TimePlugin.Date",
            Parameters = new List<OpenAIFunctionParameter>()
                    {
                        new() {
                            Name = "Format",
                            Description = "Date format",
                            ParameterType = typeof(string),
                            IsRequired = false,
                        }
                    }
        };
        this._timepluginNow = new()
        {
            FunctionName = "Now",
            PluginName = "TimePlugin",
            Description = "TimePlugin.Now",
            Parameters = new List<OpenAIFunctionParameter>()
                    {
                        new() {
                            Name = "Format",
                            Description = "Date format",
                            ParameterType = typeof(string),
                            IsRequired = false,
                        }
                    }
        };
        this._executionSettings = new()
        {
            FunctionCallBehavior = FunctionCallBehavior.EnableFunctions(new[] { this._timepluginDate, this._timepluginNow })
        };
    }

    [Fact]
    public async Task ItCreatesCorrectFunctionsWhenUsingAutoAsync()
    {
        // Arrange
        var chatCompletion = new OpenAIChatCompletionService(modelId: "gpt-3.5-turbo", apiKey: "NOKEY", httpClient: this._httpClient);
        this._messageHandlerStub.ResponseToReturn = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        { Content = new StringContent(ChatCompletionResponse) };

        // Act
        await chatCompletion.GetChatMessageContentsAsync(new ChatHistory(), this._executionSettings);

        // Assert
        var actualRequestContent = Encoding.UTF8.GetString(this._messageHandlerStub.RequestContent!);
        Assert.NotNull(actualRequestContent);
        var optionsJson = JsonSerializer.Deserialize<JsonElement>(actualRequestContent);
        Assert.Equal(2, optionsJson.GetProperty("functions").GetArrayLength());
        Assert.Equal("TimePlugin_Date", optionsJson.GetProperty("functions")[0].GetProperty("name").GetString());
        Assert.Equal("TimePlugin_Now", optionsJson.GetProperty("functions")[1].GetProperty("name").GetString());
    }

    [Fact]
    public async Task ItCreatesCorrectFunctionsWhenUsingNowAsync()
    {
        // Arrange
        var chatCompletion = new OpenAIChatCompletionService(modelId: "gpt-3.5-turbo", apiKey: "NOKEY", httpClient: this._httpClient);
        this._messageHandlerStub.ResponseToReturn = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        { Content = new StringContent(ChatCompletionResponse) };
        this._executionSettings.FunctionCallBehavior = FunctionCallBehavior.RequireFunction(this._timepluginNow);

        // Act
        await chatCompletion.GetChatMessageContentsAsync(new ChatHistory(), this._executionSettings);

        // Assert
        var actualRequestContent = Encoding.UTF8.GetString(this._messageHandlerStub.RequestContent!);
        Assert.NotNull(actualRequestContent);
        var optionsJson = JsonSerializer.Deserialize<JsonElement>(actualRequestContent);
        Assert.Equal(1, optionsJson.GetProperty("functions").GetArrayLength());
        Assert.Equal("TimePlugin_Now", optionsJson.GetProperty("functions")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task ItCreatesNoFunctionsWhenUsingNoneAsync()
    {
        // Arrange
        var chatCompletion = new OpenAIChatCompletionService(modelId: "gpt-3.5-turbo", apiKey: "NOKEY", httpClient: this._httpClient);
        this._messageHandlerStub.ResponseToReturn = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        { Content = new StringContent(ChatCompletionResponse) };
        this._executionSettings.FunctionCallBehavior = null;

        // Act
        await chatCompletion.GetChatMessageContentsAsync(new ChatHistory(), this._executionSettings);

        // Assert
        var actualRequestContent = Encoding.UTF8.GetString(this._messageHandlerStub.RequestContent!);
        Assert.NotNull(actualRequestContent);
        var optionsJson = JsonSerializer.Deserialize<JsonElement>(actualRequestContent);
        Assert.False(optionsJson.TryGetProperty("functions", out var _));
    }

    [Fact]
    public async Task ItAddsNameToChatMessageAsync()
    {
        // Arrange
        var chatCompletion = new OpenAIChatCompletionService(modelId: "gpt-3.5-turbo", apiKey: "NOKEY", httpClient: this._httpClient);
        this._messageHandlerStub.ResponseToReturn = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        { Content = new StringContent(ChatCompletionResponse) };
        var chatHistory = new ChatHistory();
        chatHistory.AddMessage(AuthorRole.User, "Hello", metadata: new Dictionary<string, object?>() { { OpenAIChatMessageContent.FunctionNameProperty, "John Doe" } });

        // Act
        await chatCompletion.GetChatMessageContentsAsync(chatHistory, this._executionSettings);

        // Assert
        var actualRequestContent = Encoding.UTF8.GetString(this._messageHandlerStub.RequestContent!);
        Assert.NotNull(actualRequestContent);
        var optionsJson = JsonSerializer.Deserialize<JsonElement>(actualRequestContent);
        Assert.Equal(1, optionsJson.GetProperty("messages").GetArrayLength());
        Assert.Equal("John Doe", optionsJson.GetProperty("messages")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task ItAddsNameAndArgumentsToChatMessageAsync()
    {
        // Arrange
        var chatCompletion = new OpenAIChatCompletionService(modelId: "gpt-3.5-turbo", apiKey: "NOKEY", httpClient: this._httpClient);
        this._messageHandlerStub.ResponseToReturn = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        { Content = new StringContent(ChatCompletionResponse) };
        var chatHistory = new ChatHistory();
        chatHistory.AddMessage(AuthorRole.User, "Hello", metadata: new Dictionary<string, object?>() { { OpenAIChatMessageContent.FunctionNameProperty, "SayHello" }, { OpenAIChatMessageContent.FunctionArgumentsProperty, "{ \"user\": \"John Doe\" }" } });

        // Act
        await chatCompletion.GetChatMessageContentsAsync(chatHistory, this._executionSettings);

        // Assert
        var actualRequestContent = Encoding.UTF8.GetString(this._messageHandlerStub.RequestContent!);
        Assert.NotNull(actualRequestContent);
        var optionsJson = JsonSerializer.Deserialize<JsonElement>(actualRequestContent);
        Assert.Equal(1, optionsJson.GetProperty("messages").GetArrayLength());
        Assert.Equal("SayHello", optionsJson.GetProperty("messages")[0].GetProperty("function_call").GetProperty("name").GetString());
        Assert.Equal("{ \"user\": \"John Doe\" }", optionsJson.GetProperty("messages")[0].GetProperty("function_call").GetProperty("arguments").GetString());

        // When both name and arguments are present, name should only be included in the function_call property.
        Assert.Throws<KeyNotFoundException>(() => optionsJson.GetProperty("messages")[0].GetProperty("name"));
    }

    [Fact]
    public async Task ItGetChatMessageContentsShouldHaveModelIdDefinedAsync()
    {
        // Arrange
        var chatCompletion = new OpenAIChatCompletionService(modelId: "gpt-3.5-turbo", apiKey: "NOKEY", httpClient: this._httpClient);
        this._messageHandlerStub.ResponseToReturn = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        { Content = new StringContent(AzureChatCompletionResponse, Encoding.UTF8, "application/json") };

        var chatHistory = new ChatHistory();
        chatHistory.AddMessage(AuthorRole.User, "Hello");

        // Act
        var chatMessage = await chatCompletion.GetChatMessageContentAsync(chatHistory, this._executionSettings);

        // Assert
        Assert.NotNull(chatMessage.ModelId);
        Assert.Equal("gpt-3.5-turbo", chatMessage.ModelId);
    }

    [Fact]
    public async Task ItGetTextContentsShouldHaveModelIdDefinedAsync()
    {
        // Arrange
        var chatCompletion = new OpenAIChatCompletionService(modelId: "gpt-3.5-turbo", apiKey: "NOKEY", httpClient: this._httpClient);
        this._messageHandlerStub.ResponseToReturn = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        { Content = new StringContent(AzureChatCompletionResponse, Encoding.UTF8, "application/json") };

        var chatHistory = new ChatHistory();
        chatHistory.AddMessage(AuthorRole.User, "Hello");

        // Act
        var textContent = await chatCompletion.GetTextContentAsync("hello", this._executionSettings);

        // Assert
        Assert.NotNull(textContent.ModelId);
        Assert.Equal("gpt-3.5-turbo", textContent.ModelId);
    }

    public void Dispose()
    {
        this._httpClient.Dispose();
        this._messageHandlerStub.Dispose();
    }

    private const string ChatCompletionResponse = @"{
  ""id"": ""chatcmpl-8IlRBQU929ym1EqAY2J4T7GGkW5Om"",
  ""object"": ""chat.completion"",
  ""created"": 1699482945,
  ""model"": ""gpt-3.5-turbo"",
  ""choices"": [
    {
      ""index"": 0,
      ""message"": {
        ""role"": ""assistant"",
        ""content"": null,
        ""function_call"": {
          ""name"": ""TimePlugin_Date"",
          ""arguments"": ""{}""
        }
      },
      ""finish_reason"": ""stop""
    }
  ],
  ""usage"": {
    ""prompt_tokens"": 52,
    ""completion_tokens"": 1,
    ""total_tokens"": 53
  }
}";
    private const string AzureChatCompletionResponse = @"{
    ""id"": ""chatcmpl-8S914omCBNQ0KU1NFtxmupZpzKWv2"",
    ""object"": ""chat.completion"",
    ""created"": 1701718534,
    ""model"": ""gpt-3.5-turbo"",
    ""prompt_filter_results"": [
        {
            ""prompt_index"": 0,
            ""content_filter_results"": {
                ""hate"": {
                    ""filtered"": false,
                    ""severity"": ""safe""
                },
                ""self_harm"": {
                    ""filtered"": false,
                    ""severity"": ""safe""
                },
                ""sexual"": {
                    ""filtered"": false,
                    ""severity"": ""safe""
                },
                ""violence"": {
                    ""filtered"": false,
                    ""severity"": ""safe""
                }
            }
        }
    ],
    ""choices"": [
        {
            ""index"": 0,
            ""finish_reason"": ""stop"",
            ""message"": {
                ""role"": ""assistant"",
                ""content"": ""Hello! How can I help you today? Please provide me with a question or topic you would like information on.""
            },
            ""content_filter_results"": {
                ""hate"": {
                    ""filtered"": false,
                    ""severity"": ""safe""
                },
                ""self_harm"": {
                    ""filtered"": false,
                    ""severity"": ""safe""
                },
                ""sexual"": {
                    ""filtered"": false,
                    ""severity"": ""safe""
                },
                ""violence"": {
                    ""filtered"": false,
                    ""severity"": ""safe""
                }
            }
        }
    ],
    ""usage"": {
        ""prompt_tokens"": 23,
        ""completion_tokens"": 23,
        ""total_tokens"": 46
    }
}";
}
