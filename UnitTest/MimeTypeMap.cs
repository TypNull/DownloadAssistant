using DownloadAssistant.Media;
using Xunit;

namespace UnitTest
{
    public class MimeTypeMapTests
    {
        [Fact]
        public void GetMimeType_ValidExtension_ReturnsCorrectMimeType()
        {
            // Arrange
            string fileName = "test.jpg";

            // Act
            string mimeType = MimeTypeMap.GetMimeType(fileName);

            // Assert
            Assert.Equal("image/jpeg", mimeType);
        }

        [Fact]
        public void GetMimeType_ExtensionWithQueryString_ReturnsCorrectMimeType()
        {
            // Arrange
            string fileName = "test.png?version=1.0";

            // Act
            string mimeType = MimeTypeMap.GetMimeType(fileName);

            // Assert
            Assert.Equal("image/png", mimeType);
        }

        [Fact]
        public void GetMimeType_NoExtension_ReturnsDefaultMimeType()
        {
            // Arrange
            string fileName = "test";

            // Act
            string mimeType = MimeTypeMap.GetMimeType(fileName);

            // Assert
            Assert.Equal("application/octet-stream", mimeType);
        }

        [Fact]
        public void GetMimeType_UnknownExtension_ReturnsDefaultMimeType()
        {
            // Arrange
            string fileName = "test.unknown";

            // Act
            string mimeType = MimeTypeMap.GetMimeType(fileName);

            // Assert
            Assert.Equal("application/octet-stream", mimeType);
        }

        [Fact]
        public void GetMimeType_NullInput_ThrowsArgumentNullException()
        {
            // Arrange
            string fileName = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => MimeTypeMap.GetMimeType(fileName));
        }

        [Fact]
        public void TryGetMimeType_ValidExtension_ReturnsTrueAndCorrectMimeType()
        {
            // Arrange
            string fileName = "test.pdf";

            // Act
            bool result = MimeTypeMap.TryGetMimeType(fileName, out string mimeType);

            // Assert
            Assert.True(result);
            Assert.Equal("application/pdf", mimeType);
        }

        [Fact]
        public void TryGetMimeType_UnknownExtension_ReturnsFalse()
        {
            // Arrange
            string fileName = "test.unknown";

            // Act
            bool result = MimeTypeMap.TryGetMimeType(fileName, out string mimeType);

            // Assert
            Assert.False(result);
            Assert.Null(mimeType);
        }

        [Fact]
        public void GetDefaultExtension_ValidMimeType_ReturnsCorrectExtension()
        {
            // Arrange
            string mimeType = "text/html";

            // Act
            string extension = MimeTypeMap.GetDefaultExtension(mimeType);

            // Assert
            Assert.Equal(".shtml", extension);
        }

        [Fact]
        public void GetDefaultExtension_UnknownMimeType_ReturnsEmptyString()
        {
            // Arrange
            string mimeType = "unknown/type";

            // Act
            string extension = MimeTypeMap.GetDefaultExtension(mimeType);

            // Assert
            Assert.Equal(string.Empty, extension);
        }

        [Fact]
        public void GetDefaultExtension_NullInput_ThrowsArgumentNullException()
        {
            // Arrange
            string mimeType = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => MimeTypeMap.GetDefaultExtension(mimeType));
        }

        [Fact]
        public void GetExtension_ValidMimeType_ReturnsCorrectExtension()
        {
            // Arrange
            string mimeType = "application/json";

            // Act
            string extension = MimeTypeMap.GetExtension(mimeType);

            // Assert
            Assert.Equal(".json", extension);
        }

        [Fact]
        public void GetExtension_UnknownMimeType_ThrowsArgumentException()
        {
            // Arrange
            string mimeType = "unknown/type";

            // Act & Assert
            Assert.Throws<ArgumentException>(() => MimeTypeMap.GetExtension(mimeType));
        }

        [Fact]
        public void GetExtension_UnknownMimeType_NoThrow_ReturnsEmptyString()
        {
            // Arrange
            string mimeType = "unknown/type";

            // Act
            string extension = MimeTypeMap.GetExtension(mimeType, throwErrorIfNotFound: false);

            // Assert
            Assert.Equal(string.Empty, extension);
        }

        [Fact]
        public void GetExtension_NullInput_ThrowsArgumentNullException()
        {
            // Arrange
            string mimeType = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => MimeTypeMap.GetExtension(mimeType));
        }

        [Fact]
        public void GetExtension_MimeTypeStartsWithDot_ThrowsArgumentException()
        {
            // Arrange
            string mimeType = ".invalid";

            // Act & Assert
            Assert.Throws<ArgumentException>(() => MimeTypeMap.GetExtension(mimeType));
        }
    }
}
