﻿using Abp.Application.Services;
using Abp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Odco.PointOfSales.Core.Common;
using Odco.PointOfSales.Core.Enums;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Odco.PointOfSales.Application.Common.SequenceNumbers
{
    public interface IDocumentSequenceNumberManager : IApplicationService
    {
        Task SyncDocumentSequenceNumber();

        string GetNextDocumentNumber(byte documentType);

        Task<string> GetAndUpdateNextDocumentNumberAsync(DocumentType documentType);
    }

    public class DocumentSequenceNumberManagerImplementation : ApplicationService, IDocumentSequenceNumberManager
    {
        private readonly IRepository<DocumentSequenceNumber, Guid> _documentSequenceNumberRepository;

        public DocumentSequenceNumberManagerImplementation(IRepository<DocumentSequenceNumber, Guid> documentSequenceNumberRepository)
        {
            _documentSequenceNumberRepository = documentSequenceNumberRepository;
        }

        public async Task SyncDocumentSequenceNumber()
        {
            foreach (var item in Enum.GetValues(typeof(DocumentType)))
            {
                var _documentNumber = (byte)item;
                var _documentDescription = item.ToString();

                var documentSequence = _documentSequenceNumberRepository.FirstOrDefault(s => s.DocumentType == (DocumentType)_documentNumber);
                if (documentSequence == null)
                {
                    documentSequence = new DocumentSequenceNumber
                    {
                        DocumentType = (DocumentType)_documentNumber,
                        Description = _documentDescription,
                        Prefix = null,
                        Suffix = null,
                        StartingNumber = 0,
                        LastNumber = 0,
                        Length = 10,
                        IsAutoGenerated = true
                    };

                    await _documentSequenceNumberRepository.InsertAsync(documentSequence);
                    await CurrentUnitOfWork.SaveChangesAsync();
                }

            }
        }

        public string GetNextDocumentNumber(byte documentType)
        {
            var sequenceNumber = _documentSequenceNumberRepository
                .GetAll()
                .FirstOrDefault(ds => ds.DocumentType == (DocumentType)documentType);

            if (sequenceNumber == null)
            {
                throw new Exception("Sequence number for document type has not being defined");
            }

            if (!sequenceNumber.IsAutoGenerated)
                return null;

            var _totalLength = sequenceNumber.Prefix != null ? sequenceNumber.Length - sequenceNumber.Prefix.Length : sequenceNumber.Length;

            return sequenceNumber.Prefix + (sequenceNumber.LastNumber + 1).ToString().PadLeft(_totalLength, '0');
        }

        public async Task<string> GetAndUpdateNextDocumentNumberAsync(DocumentType documentType)
        {
            var sequenceNumber = await _documentSequenceNumberRepository
                .GetAll()
                .FirstOrDefaultAsync(ds => ds.DocumentType == documentType);

            if (sequenceNumber == null)
            {
                throw new Exception("Sequence number for document type has not being defined");
            }

            if (!sequenceNumber.IsAutoGenerated)
                return null;

            var _totalLength = sequenceNumber.Prefix != null ? sequenceNumber.Length - sequenceNumber.Prefix.Length : sequenceNumber.Length;

            sequenceNumber.LastNumber++; // adding 1

            _documentSequenceNumberRepository.Update(sequenceNumber);

            CurrentUnitOfWork.SaveChanges();

            return sequenceNumber.Prefix + (sequenceNumber.LastNumber).ToString().PadLeft(_totalLength, '0');
        }

    }
}
