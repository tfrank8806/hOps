using System;

namespace hOps.web.Models
{
    public class EmailAttachment
    {
        public EmailAttachment(string fileName, byte[] content, string contentType)
        {
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            Content = content ?? throw new ArgumentNullException(nameof(content));
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
        }

        public string FileName { get; }

        public byte[] Content { get; }

        public string ContentType { get; }
    }
}
