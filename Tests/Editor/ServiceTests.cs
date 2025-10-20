using System;
using NUnit.Framework;
using Akira.Tools.Services;
using Akira.Tools.Core;
using System.Collections.Generic;
using UnityEngine;

namespace Akira.Tools.Tests
{
    /// <summary>
    /// Unit tests for PackageService
    /// </summary>
    public class PackageServiceTests
    {
        private PackageService packageService;

        [SetUp]
        public void SetUp()
        {
            packageService = new PackageService();
            ErrorHandler.DebugMode = false; // Disable debug logs during tests
        }

        [TearDown]
        public void TearDown()
        {
            packageService = null;
        }

        #region Git URL Validation Tests

        [Test]
        public void ParseAssetStoreUrl_ValidUrl_ReturnsPackageInfo()
        {
            // Arrange
            string validUrl = "https://assetstore.unity.com/packages/tools/utilities/test-package-12345";

            // Act
            var result = packageService.ParseAssetStoreUrl(validUrl);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(12345, result.PackageId);
            Assert.AreEqual(validUrl, result.Url);
        }

        [Test]
        public void ParseAssetStoreUrl_InvalidUrl_ReturnsNull()
        {
            // Arrange
            string invalidUrl = "https://example.com/packages/12345";

            // Act
            var result = packageService.ParseAssetStoreUrl(invalidUrl);

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void ParseAssetStoreUrl_EmptyUrl_ReturnsNull()
        {
            // Arrange
            string emptyUrl = "";

            // Act
            var result = packageService.ParseAssetStoreUrl(emptyUrl);

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void ParseAssetStoreUrl_NullUrl_ReturnsNull()
        {
            // Arrange
            string nullUrl = null;

            // Act
            var result = packageService.ParseAssetStoreUrl(nullUrl);

            // Assert
            Assert.IsNull(result);
        }

        #endregion

        #region Package Installation Tests

        [Test]
        public void InstallPackageFromGit_EmptyUrl_InvokesCallbackWithFailure()
        {
            // Arrange
            bool callbackInvoked = false;
            bool success = true;
            string message = "";

            // Act
            packageService.InstallPackageFromGit("", (s, m) =>
            {
                callbackInvoked = true;
                success = s;
                message = m;
            });

            // Assert
            Assert.IsTrue(callbackInvoked);
            Assert.IsFalse(success);
            Assert.IsNotEmpty(message);
        }

        [Test]
        public void InstallPackage_EmptyName_InvokesCallbackWithFailure()
        {
            // Arrange
            bool callbackInvoked = false;
            bool success = true;
            string message = "";

            // Act
            packageService.InstallPackage("", (s, m) =>
            {
                callbackInvoked = true;
                success = s;
                message = m;
            });

            // Assert
            Assert.IsTrue(callbackInvoked);
            Assert.IsFalse(success);
            Assert.IsNotEmpty(message);
        }

        [Test]
        public void RemovePackage_EmptyName_InvokesCallbackWithFailure()
        {
            // Arrange
            bool callbackInvoked = false;
            bool success = true;
            string message = "";

            // Act
            packageService.RemovePackage("", (s, m) =>
            {
                callbackInvoked = true;
                success = s;
                message = m;
            });

            // Assert
            Assert.IsTrue(callbackInvoked);
            Assert.IsFalse(success);
            Assert.IsNotEmpty(message);
        }

        #endregion
    }

    /// <summary>
    /// Unit tests for FolderService
    /// </summary>
    public class FolderServiceTests
    {
        private FolderService folderService;

        [SetUp]
        public void SetUp()
        {
            folderService = new FolderService();
            ErrorHandler.DebugMode = false;
        }

        [TearDown]
        public void TearDown()
        {
            folderService = null;
        }

        #region Preset Tests

        [Test]
        public void GetAllPresets_ReturnsBuiltInPresets()
        {
            // Act
            var presets = folderService.GetAllPresets();

            // Assert
            Assert.IsNotNull(presets);
            Assert.IsTrue(presets.Count > 0);
            Assert.IsTrue(presets.Exists(p => p.Name == "Standard Project"));
            Assert.IsTrue(presets.Exists(p => p.Name == "Mobile Game"));
            Assert.IsTrue(presets.Exists(p => p.Name == "VR Project"));
        }

        [Test]
        public void GetAllPresets_CachesResults()
        {
            // Act
            var presets1 = folderService.GetAllPresets();
            var presets2 = folderService.GetAllPresets();

            // Assert
            Assert.AreSame(presets1, presets2);
        }

        [Test]
        public void LoadPreset_NonExistentPreset_ReturnsNull()
        {
            // Act
            var preset = folderService.LoadPreset("NonExistentPreset");

            // Assert
            Assert.IsNull(preset);
        }

        [Test]
        public void LoadPreset_EmptyName_ReturnsNull()
        {
            // Act
            var preset = folderService.LoadPreset("");

            // Assert
            Assert.IsNull(preset);
        }

        #endregion

        #region Folder Structure Tests

        [Test]
        public void CreateFolderStructure_NullList_InvokesCallbackWithFailure()
        {
            // Arrange
            bool callbackInvoked = false;
            bool success = true;

            // Act
            folderService.CreateFolderStructure(null, (s, m) =>
            {
                callbackInvoked = true;
                success = s;
            });

            // Assert
            Assert.IsTrue(callbackInvoked);
            Assert.IsFalse(success);
        }

        [Test]
        public void CreateFolderStructure_EmptyList_InvokesCallbackWithFailure()
        {
            // Arrange
            bool callbackInvoked = false;
            bool success = true;
            var emptyList = new List<string>();

            // Act
            folderService.CreateFolderStructure(emptyList, (s, m) =>
            {
                callbackInvoked = true;
                success = s;
            });

            // Assert
            Assert.IsTrue(callbackInvoked);
            Assert.IsFalse(success);
        }

        [Test]
        public void ValidateFolderStructure_EmptyList_ReturnsTrue()
        {
            // Arrange
            var emptyList = new List<string>();

            // Act
            var isValid = folderService.ValidateFolderStructure(emptyList, out var missing);

            // Assert
            Assert.IsTrue(isValid);
            Assert.AreEqual(0, missing.Count);
        }

        [Test]
        public void CreateFromPreset_NullPreset_InvokesCallbackWithFailure()
        {
            // Arrange
            bool callbackInvoked = false;
            bool success = true;

            // Act
            folderService.CreateFromPreset(null, "Assets", (s, m) =>
            {
                callbackInvoked = true;
                success = s;
            });

            // Assert
            Assert.IsTrue(callbackInvoked);
            Assert.IsFalse(success);
        }

        #endregion

        #region Preset Management Tests

        [Test]
        public void SavePreset_NullPreset_InvokesCallbackWithFailure()
        {
            // Arrange
            bool callbackInvoked = false;
            bool success = true;

            // Act
            folderService.SavePreset(null, (s, m) =>
            {
                callbackInvoked = true;
                success = s;
            });

            // Assert
            Assert.IsTrue(callbackInvoked);
            Assert.IsFalse(success);
        }

        [Test]
        public void DeletePreset_EmptyName_InvokesCallbackWithFailure()
        {
            // Arrange
            bool callbackInvoked = false;
            bool success = true;

            // Act
            folderService.DeletePreset("", (s, m) =>
            {
                callbackInvoked = true;
                success = s;
            });

            // Assert
            Assert.IsTrue(callbackInvoked);
            Assert.IsFalse(success);
        }

        #endregion
    }

    /// <summary>
    /// Unit tests for ErrorHandler
    /// </summary>
    public class ErrorHandlerTests
    {
        [SetUp]
        public void SetUp()
        {
            ErrorHandler.DebugMode = false;
        }

        #region Validation Tests

        [Test]
        public void Validate_TrueCondition_ReturnsTrue()
        {
            // Act
            bool result = ErrorHandler.Validate(true, "Error message");

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void Validate_FalseCondition_ReturnsFalse()
        {
            // Act
            bool result = ErrorHandler.Validate(false, "Error message");

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void ValidateNotNull_NotNullObject_ReturnsTrue()
        {
            // Arrange
            var obj = new object();

            // Act
            bool result = ErrorHandler.ValidateNotNull(obj, "TestObject");

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void ValidateNotNull_NullObject_ReturnsFalse()
        {
            // Act
            bool result = ErrorHandler.ValidateNotNull(null, "TestObject");

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void ValidateNotEmpty_ValidString_ReturnsTrue()
        {
            // Act
            bool result = ErrorHandler.ValidateNotEmpty("test", "TestString");

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void ValidateNotEmpty_EmptyString_ReturnsFalse()
        {
            // Act
            bool result = ErrorHandler.ValidateNotEmpty("", "TestString");

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void ValidateNotEmpty_NullString_ReturnsFalse()
        {
            // Act
            bool result = ErrorHandler.ValidateNotEmpty(null, "TestString");

            // Assert
            Assert.IsFalse(result);
        }

        #endregion

        #region Try-Catch Tests

        [Test]
        public void Try_SuccessfulAction_ExecutesWithoutError()
        {
            // Arrange
            bool actionExecuted = false;
            bool errorHandlerCalled = false;

            // Act
            ErrorHandler.Try(
                () => { actionExecuted = true; },
                (ex) => { errorHandlerCalled = true; }
            );

            // Assert
            Assert.IsTrue(actionExecuted);
            Assert.IsFalse(errorHandlerCalled);
        }

        [Test]
        public void Try_ThrowingAction_CatchesException()
        {
            // Arrange
            bool errorHandlerCalled = false;
            Exception caughtException = null;

            // Act
            ErrorHandler.Try(
                () => { throw new System.Exception("Test exception"); },
                (ex) =>
                {
                    errorHandlerCalled = true;
                    caughtException = ex;
                }
            );

            // Assert
            Assert.IsTrue(errorHandlerCalled);
            Assert.IsNotNull(caughtException);
            Assert.AreEqual("Test exception", caughtException.Message);
        }

        [Test]
        public void TryFunc_SuccessfulFunction_ReturnsResult()
        {
            // Act
            int result = ErrorHandler.Try(() => 42, defaultValue: 0);

            // Assert
            Assert.AreEqual(42, result);
        }

        [Test]
        public void TryFunc_ThrowingFunction_ReturnsDefaultValue()
        {
            // Act
            int result = ErrorHandler.Try<int>(
                () => { throw new System.Exception("Test exception"); },
                defaultValue: -1
            );

            // Assert
            Assert.AreEqual(-1, result);
        }

        #endregion

        #region Error Messages Tests

        [Test]
        public void GetErrorMessage_ValidCode_ReturnsMessage()
        {
            // Act
            string message = ErrorHandler.GetErrorMessage("PKG001");

            // Assert
            Assert.IsNotEmpty(message);
            Assert.IsFalse(message.Contains("unknown"));
        }

        [Test]
        public void GetErrorMessage_InvalidCode_ReturnsDefaultMessage()
        {
            // Act
            string message = ErrorHandler.GetErrorMessage("INVALID_CODE");

            // Assert
            Assert.IsNotEmpty(message);
            Assert.IsTrue(message.Contains("unknown"));
        }

        #endregion
    }
}

