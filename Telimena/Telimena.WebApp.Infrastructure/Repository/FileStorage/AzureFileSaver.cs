﻿using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using DotNetLittleHelpers;
using Microsoft.WindowsAzure.Storage.Blob;
using Telimena.WebApp.Core.Models;

namespace Telimena.WebApp.Infrastructure.Repository.FileStorage
{
    public class AzureFileSaver : AzureStorageConnectorBase, IFileSaver
    {
        public async Task SaveFile(IRepositoryFile file, Stream fileStream, string containerName)
        {
            CloudBlobContainer container = await this.GetContainer(containerName, this.ConnectionStringName);

            CloudBlockBlob blockBlob = container.GetBlockBlobReference(file.FileName);
            blockBlob.Properties.ContentType = this.ContentType;
            fileStream.Position = 0;

            await blockBlob.UploadFromStreamAsync(fileStream);

            file.FileLocation = blockBlob.Uri?.ToString();
        }
       

    }
}