using System;
using System.Linq;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Kiểm thử toàn diện ViewModel ZigbeeConfigViewModel theo chuẩn IEC 61508-3 Triad:
/// 1. Nominal Input (Contract Execution)
/// 2. Tolerant Normalization (Permissible Variants)
/// 3. Adversarial / Negative Input (Safety Gate & Auto-Revert)
/// </summary>
public class ZigbeeConfigViewModelTests
{
    // ==========================================
    // 1. NOMINAL INPUT TESTS
    // ==========================================

    [Fact]
    public void ZigbeeConfigViewModel_DefaultState_InitializesSafely()
    {
        // Arrange & Act
        var vm = new ZigbeeConfigViewModel();

        // Assert
        Assert.False(vm.IsEnabled);
        Assert.Equal(ZigbeeRole.Slave, vm.DeviceRole);
        Assert.True(vm.IsRoleSlave);
        Assert.False(vm.IsRoleMaster);
        Assert.Equal((byte)1, vm.StationId);
        Assert.Equal("1", vm.StationIdText);
        Assert.Equal((ushort)0x1A2B, vm.PanId);
        Assert.Equal("1A2B", vm.PanIdHex);
        Assert.Equal((ushort)15, vm.Channel);
        Assert.True(vm.IsCleanChannel);
        Assert.Equal(ZigbeeAntenna.Pcb, vm.AntennaMode);
        Assert.True(vm.IsAntennaPcb);
        Assert.False(vm.IsAntennaExternal);
        Assert.False(vm.IsSecurityEnabled);
        Assert.True(vm.PreserveKeyOnDeploy);
        Assert.False(vm.IsKeySetInFlash);
    }

    [Fact]
    public void ZigbeeConfigViewModel_LoadFromDto_MapsAllFieldsCorrectly()
    {
        // Arrange
        var vm = new ZigbeeConfigViewModel();
        var dto = new ZigbeeConfigDto
        {
            Enabled = true,
            Role = ZigbeeRole.Master,
            SlaveId = 10,
            PanId = 0xABCD,
            Channel = 20,
            Antenna = ZigbeeAntenna.External,
            SecurityEnabled = true,
            PreserveKey = false,
            LinkState = ZigbeeLinkState.Up,
            Rssi = -64,
            NodeCount = 5,
            IsKeySet = true,
            RawErrorCode = 0
        };

        // Act
        vm.LoadFromDto(dto);

        // Assert
        Assert.True(vm.IsEnabled);
        Assert.Equal(ZigbeeRole.Master, vm.DeviceRole);
        Assert.True(vm.IsRoleMaster);
        Assert.Equal((byte)10, vm.StationId);
        Assert.Equal("10", vm.StationIdText);
        Assert.Equal((ushort)0xABCD, vm.PanId);
        Assert.Equal("ABCD", vm.PanIdHex);
        Assert.Equal((ushort)20, vm.Channel);
        Assert.True(vm.IsCleanChannel);
        Assert.Equal(ZigbeeAntenna.External, vm.AntennaMode);
        Assert.True(vm.IsAntennaExternal);
        Assert.True(vm.IsSecurityEnabled);
        Assert.True(vm.IsKeySetInFlash);
        Assert.Equal(ZigbeeLinkState.Up, vm.LinkState);
        Assert.True(vm.IsLinkUp);
        Assert.Equal((short)-64, vm.RssiDbm);
        Assert.Equal("-64 dBm", vm.RssiDisplayText);
        Assert.Equal((ushort)5, vm.NodeCount);
    }

    [Fact]
    public void ZigbeeConfigViewModel_ExportToDto_PreservesWireContract()
    {
        // Arrange
        var vm = new ZigbeeConfigViewModel
        {
            IsEnabled = true,
            DeviceRole = ZigbeeRole.Slave,
            Channel = 25,
            AntennaMode = ZigbeeAntenna.External,
            IsSecurityEnabled = true,
            PreserveKeyOnDeploy = true
        };
        vm.StationIdText = "42";
        vm.CommitStationId();
        vm.PanIdHex = "5E6F";
        vm.CommitPanId();

        // Act
        var dto = vm.ExportToDto();

        // Assert
        Assert.True(dto.Enabled);
        Assert.Equal(ZigbeeRole.Slave, dto.Role);
        Assert.Equal((byte)42, dto.SlaveId);
        Assert.Equal((ushort)0x5E6F, dto.PanId);
        Assert.Equal((ushort)25, dto.Channel);
        Assert.Equal(ZigbeeAntenna.External, dto.Antenna);
        Assert.True(dto.SecurityEnabled);
        Assert.True(dto.PreserveKey);
    }

    [Fact]
    public void ZigbeeConfigViewModel_ModelSync_SavesAndLoadsWithoutDataLoss()
    {
        // Arrange
        var vm = new ZigbeeConfigViewModel
        {
            IsEnabled = true,
            DeviceRole = ZigbeeRole.Master,
            Channel = 26,
            AntennaMode = ZigbeeAntenna.External,
            IsSecurityEnabled = true,
            PreserveKeyOnDeploy = false,
            NetworkName = "MeshCoordinator"
        };
        vm.StationIdText = "5";
        vm.CommitStationId();
        vm.PanIdHex = "FEED";
        vm.CommitPanId();

        var model = new ZigbeeConfigModel();

        // Act
        vm.SaveToModel(model);

        var vm2 = new ZigbeeConfigViewModel();
        vm2.LoadFromModel(model);

        // Assert
        Assert.True(vm2.IsEnabled);
        Assert.Equal(ZigbeeRole.Master, vm2.DeviceRole);
        Assert.Equal((byte)5, vm2.StationId);
        Assert.Equal("FEED", vm2.PanIdHex);
        Assert.Equal((ushort)26, vm2.Channel);
        Assert.Equal(ZigbeeAntenna.External, vm2.AntennaMode);
        Assert.True(vm2.IsSecurityEnabled);
        Assert.False(vm2.PreserveKeyOnDeploy);
        Assert.Equal("MeshCoordinator", vm2.NetworkName);
    }

    // ==========================================
    // 2. TOLERANT NORMALIZATION TESTS
    // ==========================================

    [Theory]
    [InlineData("1a2b", "1A2B", (ushort)0x1A2B)]
    [InlineData("0x1A2B", "1A2B", (ushort)0x1A2B)]
    [InlineData("  cafe  ", "CAFE", (ushort)0xCAFE)]
    public void ZigbeeConfigViewModel_CommitPanId_NormalizesHexCaseAndPrefix(string input, string expectedHex, ushort expectedValue)
    {
        // Arrange
        var vm = new ZigbeeConfigViewModel();
        vm.PanIdHex = input;

        // Act
        vm.CommitPanId();

        // Assert
        Assert.Equal(expectedHex, vm.PanIdHex);
        Assert.Equal(expectedValue, vm.PanId);
    }

    [Theory]
    [InlineData((ushort)15, true)]
    [InlineData((ushort)20, true)]
    [InlineData((ushort)25, true)]
    [InlineData((ushort)26, true)]
    [InlineData((ushort)11, false)]
    [InlineData((ushort)16, false)]
    public void ZigbeeConfigViewModel_ChannelSelection_IdentifiesCleanChannels(ushort channel, bool expectedIsClean)
    {
        // Arrange
        var vm = new ZigbeeConfigViewModel();

        // Act
        vm.Channel = channel;

        // Assert
        Assert.Equal(expectedIsClean, vm.IsCleanChannel);
    }

    // ==========================================
    // 3. ADVERSARIAL & SAFETY GATE TESTS
    // ==========================================

    [Theory]
    [InlineData("236")]
    [InlineData("237")]
    public void ZigbeeConfigViewModel_StationIdDrafting_BlocksRestrictedIds236And237(string restrictedId)
    {
        // Arrange
        var vm = new ZigbeeConfigViewModel();

        // Act
        vm.StationIdText = restrictedId;

        // Assert
        Assert.True(vm.HasStationIdError);
        Assert.False(string.IsNullOrWhiteSpace(vm.StationIdErrorMessage));
    }

    [Theory]
    [InlineData("236")]
    [InlineData("237")]
    [InlineData("0")]
    [InlineData("248")]
    [InlineData("-5")]
    [InlineData("abc")]
    public void ZigbeeConfigViewModel_CommitStationId_WithInvalidOrRestrictedInput_AutoReverts(string badInput)
    {
        // Arrange
        var vm = new ZigbeeConfigViewModel();
        vm.StationIdText = "10";
        vm.CommitStationId();
        Assert.Equal((byte)10, vm.StationId);

        // Act
        vm.StationIdText = badInput;
        vm.CommitStationId();

        // Assert
        Assert.Equal("10", vm.StationIdText);
        Assert.Equal((byte)10, vm.StationId);
        Assert.False(vm.HasStationIdError);
    }

    [Theory]
    [InlineData("0000")] // 0x0000 is invalid
    [InlineData("FFFF")] // 0xFFFF is broadcast, invalid for PAN ID
    [InlineData("XYZ")]  // Non-hex
    public void ZigbeeConfigViewModel_CommitPanId_WithOutOfRangeOrInvalidInput_AutoReverts(string badPan)
    {
        // Arrange
        var vm = new ZigbeeConfigViewModel();
        vm.PanIdHex = "1A2B";
        vm.CommitPanId();

        // Act
        vm.PanIdHex = badPan;
        vm.CommitPanId();

        // Assert
        Assert.Equal("1A2B", vm.PanIdHex);
        Assert.Equal((ushort)0x1A2B, vm.PanId);
    }

    [Fact]
    public void ZigbeeConfigViewModel_PreflightValidate_WhenRestrictedStationId_FailsWithErrorMessage()
    {
        // Arrange
        var vm = new ZigbeeConfigViewModel();
        vm.StationIdText = "236";

        // Act
        bool isValid = vm.PreflightValidate(out string? error);

        // Assert
        Assert.False(isValid);
        Assert.NotNull(error);
        Assert.Contains("236", error);
    }

    [Fact]
    public void ZigbeeConfigViewModel_AvailableChannels_ContainsAll16Channels_AndAnnotatesCleanChannels()
    {
        // Arrange & Act
        var vm = new ZigbeeConfigViewModel();

        // Assert
        Assert.Equal(16, vm.AvailableChannels.Count);
        var cleanChannels = new ushort[] { 15, 20, 25, 26 };

        foreach (var option in vm.AvailableChannels)
        {
            if (cleanChannels.Contains(option.Channel))
            {
                Assert.True(
                    option.DisplayName.Contains("Clean") || option.DisplayName.Contains("Kênh sạch"),
                    $"Channel {option.Channel} must be annotated as a clean channel.");
            }
            else
            {
                Assert.Equal(option.Channel.ToString(), option.DisplayName);
            }
        }
    }

    [Theory]
    [InlineData((ushort)15, true)]
    [InlineData((ushort)20, true)]
    [InlineData((ushort)25, true)]
    [InlineData((ushort)26, true)]
    [InlineData((ushort)11, false)]
    [InlineData((ushort)12, false)]
    [InlineData((ushort)18, false)]
    public void ZigbeeConfigViewModel_ChannelSelection_TracksCleanChannelState(ushort channel, bool expectedClean)
    {
        // Arrange
        var vm = new ZigbeeConfigViewModel();

        // Act
        vm.Channel = channel;

        // Assert
        Assert.Equal(expectedClean, vm.IsCleanChannel);
    }
}
