#nullable enable

using Xunit;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Serializes test classes that read or write the process-wide
/// <see cref="DxfContourStudio.Application.Localization.LocalizationService.Instance"/>
/// culture. Without it, LocalizationTests briefly switches the singleton to
/// en-US while NodeEditingSessionTests asserts zh-CN status text, making the
/// assertion flaky under xUnit parallelism.
/// </summary>
[CollectionDefinition("LocalizationShared", DisableParallelization = true)]
public sealed class LocalizationSharedDefinition;