using System.Net;
using System.Text.Json;

using FlowGate.Exceptions;
using FlowGate.Shared.Keenetic;
using FlowGate.Shared.Keenetic.Models;
using FlowGate.Tests.Common.Fixtures;
using FlowGate.Tests.Common.Http;

namespace FlowGate.Tests.Unit.Keenetic;

/// <summary>
///     Тесты <see cref="KeeneticRciClient"/> на ответах реального роутера
/// </summary>
public class KeeneticRciClientTests
{
    private static readonly Uri RciBaseAddress = new("http://127.0.0.1:79/rci/");

    /// <summary>
    ///     GET идёт по пути относительно /rci/ и возвращает JSON
    /// </summary>
    [Fact]
    public async Task GetAsync_ValidPath_RequestsRciPathAndReturnsJson()
    {
        var handler = FakeHttpMessageHandler.RespondJson(RciFixtures.ShowVersion);
        var sut = CreateClient(handler);

        var result = await sut.GetAsync("/show/version", TestContext.Current.CancellationToken);

        Assert.Equal("5.02.B.2.0-2", result.GetProperty("release").GetString());
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(new Uri("http://127.0.0.1:79/rci/show/version"), request.Uri);
    }

    /// <summary>
    ///     Команды уходят одним POST в виде массива {"parse": ...}
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_Commands_SendsParseArrayInOneRequest()
    {
        var handler = FakeHttpMessageHandler.RespondJson(RciFixtures.ParseGroupCreated);
        var sut = CreateClient(handler);
        string[] commands =
        [
            "object-group fqdn fgspike-test",
            "object-group fqdn fgspike-test description \"FlowGate spike\"",
            "object-group fqdn fgspike-test include example.com",
        ];

        await sut.ExecuteAsync(commands, TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(RciBaseAddress, request.Uri);
        using var body = JsonDocument.Parse(request.Body!);
        var sent = body.RootElement.EnumerateArray().Select(item => item.GetProperty("parse").GetString()).ToArray();
        Assert.Equal(commands, sent);
    }

    /// <summary>
    ///     Успешные команды разбираются с кодами, сообщениями и prompt
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_SuccessfulCommands_ParsesStatuses()
    {
        var sut = CreateClient(FakeHttpMessageHandler.RespondJson(RciFixtures.ParseGroupCreated));

        var result = await sut.ExecuteAsync(["a", "b", "c"], TestContext.Current.CancellationToken);

        Assert.False(result.HasErrors);
        Assert.Equal(3, result.Commands.Count);
        Assert.Equal("(config-ogrp-fqdn)", result.Commands[0].Prompt);
        var status = Assert.Single(result.Commands[0].Statuses);
        Assert.Equal("101581000", status.Code);
        Assert.Equal("Network::ObjectGroup", status.Ident);
        Assert.Equal("\"fgspike-test\": group created.", status.Message);
    }

    /// <summary>
    ///     Ошибка посередине пакета помечает только эту команду, остальные считаются выполненными
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ErrorInMiddle_ReportsOnlyFailedCommand()
    {
        var sut = CreateClient(FakeHttpMessageHandler.RespondJson(RciFixtures.ParseMixedError));

        var result = await sut.ExecuteAsync(["first", "bad", "third"], TestContext.Current.CancellationToken);

        Assert.True(result.HasErrors);
        var failed = Assert.Single(result.Failed);
        Assert.Equal("bad", failed.Command);
        Assert.True(failed.HasErrorCode(RciErrorCodes.NoSuchCommand));
        Assert.False(result.Commands[0].IsError);
        Assert.False(result.Commands[2].IsError);
    }

    /// <summary>
    ///     EnsureSuccess бросает исключение со списком отклонённых команд
    /// </summary>
    [Fact]
    public async Task EnsureSuccess_ErrorInBatch_ThrowsKeeneticCommandException()
    {
        var sut = CreateClient(FakeHttpMessageHandler.RespondJson(RciFixtures.ParseMixedError));
        var result = await sut.ExecuteAsync(["first", "bad", "third"], TestContext.Current.CancellationToken);

        var exception = Assert.Throws<KeeneticCommandException>(() => result.EnsureSuccess());

        var failure = Assert.Single(exception.Failures);
        Assert.Contains("bad", failure);
        Assert.Contains(RciErrorCodes.NoSuchCommand, failure);
    }

    /// <summary>
    ///     Допустимые ошибки не считаются сбоем
    /// </summary>
    [Fact]
    public async Task EnsureSuccess_OnlyExpectedErrors_DoesNotThrow()
    {
        var sut = CreateClient(FakeHttpMessageHandler.RespondJson(RciFixtures.ParseMixedError));
        var result = await sut.ExecuteAsync(["first", "bad", "third"], TestContext.Current.CancellationToken);

        var returned = result.EnsureSuccess(command => command.HasErrorCode(RciErrorCodes.NoSuchCommand));

        Assert.Same(result, returned);
    }

    /// <summary>
    ///     Пустой пакет не отправляется
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_NoCommands_DoesNotCallRci()
    {
        var handler = FakeHttpMessageHandler.RespondJson("[]");
        var sut = CreateClient(handler);

        var result = await sut.ExecuteAsync([], TestContext.Current.CancellationToken);

        Assert.Empty(result.Commands);
        Assert.Empty(handler.Requests);
    }

    /// <summary>
    ///     Число результатов не совпадает с числом команд — ответ считается некорректным
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ResultCountMismatch_ThrowsKeeneticUnavailable()
    {
        var sut = CreateClient(FakeHttpMessageHandler.RespondJson(RciFixtures.ParseGroupCreated));

        await Assert.ThrowsAsync<KeeneticUnavailableException>(
            () => sut.ExecuteAsync(["a", "b"], TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     HTTP-ошибка RCI превращается в KeeneticUnavailableException
    /// </summary>
    [Fact]
    public async Task GetAsync_HttpError_ThrowsKeeneticUnavailable()
    {
        var sut = CreateClient(FakeHttpMessageHandler.RespondJson("{}", HttpStatusCode.InternalServerError));

        var exception = await Assert.ThrowsAsync<KeeneticUnavailableException>(
            () => sut.GetAsync("show/version", TestContext.Current.CancellationToken));

        Assert.Contains("500", exception.Message);
    }

    /// <summary>
    ///     Недоступный RCI превращается в KeeneticUnavailableException
    /// </summary>
    [Fact]
    public async Task GetAsync_ConnectionRefused_ThrowsKeeneticUnavailable()
    {
        var sut = CreateClient(FakeHttpMessageHandler.Throw(new HttpRequestException("Connection refused")));

        await Assert.ThrowsAsync<KeeneticUnavailableException>(
            () => sut.GetAsync("show/version", TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Некорректный JSON превращается в KeeneticUnavailableException
    /// </summary>
    [Fact]
    public async Task GetAsync_InvalidJson_ThrowsKeeneticUnavailable()
    {
        var sut = CreateClient(FakeHttpMessageHandler.RespondJson("<html>"));

        await Assert.ThrowsAsync<KeeneticUnavailableException>(
            () => sut.GetAsync("show/version", TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Сохранение конфигурации отправляет system configuration save и бросает исключение при отказе
    /// </summary>
    [Fact]
    public async Task SaveConfigurationAsync_Rejected_ThrowsKeeneticCommandException()
    {
        var handler = FakeHttpMessageHandler.RespondJson(RciFixtures.ParseNoSuchCommand);
        var sut = CreateClient(handler);

        await Assert.ThrowsAsync<KeeneticCommandException>(
            () => sut.SaveConfigurationAsync(TestContext.Current.CancellationToken));

        var request = Assert.Single(handler.Requests);
        Assert.Contains(KeeneticRciClient.SaveConfigurationCommand, request.Body);
    }

    private static KeeneticRciClient CreateClient(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = RciBaseAddress });
}
