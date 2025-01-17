﻿using Abp.Application.Services;
using Abp.Application.Services.Dto;
using Abp.Collections.Extensions;
using Abp.Domain.Repositories;
using Abp.Extensions;
using Abp.Linq;
using Abp.Linq.Extensions;
using Microsoft.EntityFrameworkCore;
using Odco.PointOfSales.Application.Common.SequenceNumbers;
using Odco.PointOfSales.Application.Inventory.GoodsReceiveNotes;
using Odco.PointOfSales.Application.StockBalances;
using Odco.PointOfSales.Core.Enums;
using Odco.PointOfSales.Core.Inventory;
using Odco.PointOfSales.Core.Productions;
using Odco.PointOfSales.Core.StoredProcedures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Odco.PointOfSales.Application.Inventory
{
    public class InventoryAppService : ApplicationService, IInventoryAppService
    {
        private readonly IAsyncQueryableExecuter _asyncQueryableExecuter;
        private readonly IRepository<GoodsReceived, Guid> _goodsReceivedRepository;
        private readonly IRepository<StockBalance, Guid> _stockBalanceRepository;
        private readonly IDocumentSequenceNumberManager _documentSequenceNumberManager;
        private readonly IRepository<Product, Guid> _productRepository;
        private readonly IRepository<Warehouse, Guid> _warehouseRepository;
        private readonly IStoredProcedureAppService _storedProcedureAppService;
        private readonly IStockBalanceAppService _stockBalanceAppService;

        public InventoryAppService(IRepository<GoodsReceived, Guid> goodsReceivedRepository,
            IRepository<StockBalance, Guid> stockBalanceRepository,
            IDocumentSequenceNumberManager documentSequenceNumberManager,
            IRepository<Product, Guid> productRepository,
            IRepository<Warehouse, Guid> warehouseRepository,
            IStoredProcedureAppService storedProcedureAppService,
            IStockBalanceAppService stockBalanceAppService)
        {
            _asyncQueryableExecuter = NullAsyncQueryableExecuter.Instance;
            _goodsReceivedRepository = goodsReceivedRepository;
            _stockBalanceRepository = stockBalanceRepository;
            _documentSequenceNumberManager = documentSequenceNumberManager;
            _productRepository = productRepository;
            _warehouseRepository = warehouseRepository;
            _storedProcedureAppService = storedProcedureAppService;
            _stockBalanceAppService = stockBalanceAppService;
        }

        #region Goods Received Notes
        public async Task<GoodsReceivedDto> CreateGoodsReceivedNoteAsync(CreateGoodsReceivedDto input)
        {
            try
            {
                input.GoodsReceivedNumber = await _documentSequenceNumberManager.GetAndUpdateNextDocumentNumberAsync(DocumentType.GoodsReceivedNote);

                var transaction = ObjectMapper.Map<GoodsReceived>(input);

                var created = await _goodsReceivedRepository.InsertAsync(transaction);

                created.TransactionStatus = TransactionStatus.Approved;

                foreach (var lineLevel in created.GoodsReceivedProducts)
                {
                    lineLevel.GoodsRecievedNumber = created.GoodsReceivedNumber;

                    var stockBalances = await _stockBalanceAppService.GetStockBalancesAsync(lineLevel.ProductId, lineLevel.WarehouseId);

                    /// Creating a new row in StockBalance table
                    var newStockBalance = new StockBalance
                    {
                        SequenceNumber = stockBalances.Select(sb => sb.SequenceNumber).Max() + 1, // Get the heigest value + increase
                        ProductId = lineLevel.ProductId,
                        ProductCode = lineLevel.ProductCode,
                        ProductName = lineLevel.ProductName,
                        WarehouseId = lineLevel.WarehouseId,
                        WarehouseCode = lineLevel.WarehouseCode,
                        WarehouseName = lineLevel.WarehouseName,
                        BatchNumber = lineLevel.BatchNumber,
                        ExpiryDate = lineLevel.ExpiryDate,
                        BookBalanceQuantity = lineLevel.Quantity + lineLevel.FreeQuantity,
                        BookBalanceUnitOfMeasureUnit = null,
                        OnOrderQuantity = 0,
                        OnOrderQuantityUnitOfMeasureUnit = null,
                        AllocatedQuantity = 0,
                        AllocatedQuantityUnitOfMeasureUnit = null,
                        ReceivedQuantity = lineLevel.Quantity + lineLevel.FreeQuantity,
                        ReceivedQuantityUnitOfMeasureUnit = null,
                        CostPrice = lineLevel.CostPrice,
                        SellingPrice = lineLevel.SellingPrice,
                        MaximumRetailPrice = lineLevel.MaximumRetailPrice,
                        GoodsRecievedNumber = created.GoodsReceivedNumber
                    };

                    await _stockBalanceRepository.InsertAsync(newStockBalance);

                    // Company Summary + Warehouse Summary
                    var stockSummaries = stockBalances.Where(sb => sb.SequenceNumber == 0);

                    foreach (var stockSummary in stockSummaries)
                    {
                        stockSummary.BookBalanceQuantity += lineLevel.Quantity + lineLevel.FreeQuantity;
                        stockSummary.ReceivedQuantity += lineLevel.Quantity + lineLevel.FreeQuantity;

                        await _stockBalanceRepository.UpdateAsync(stockSummary);
                    }
                }
                await CurrentUnitOfWork.SaveChangesAsync();
                return ObjectMapper.Map<GoodsReceivedDto>(created);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<PagedResultDto<GoodsReceivedDto>> GetAllGoodsReceivedProductsAsync(PagedGRNResultRequestDto input)
        {
            try
            {
                var query = _goodsReceivedRepository.GetAll()
                        .WhereIf(!input.Keyword.IsNullOrWhiteSpace(),
                        x => x.GoodsReceivedNumber.Contains(input.Keyword));

                var result = _asyncQueryableExecuter.ToListAsync
                    (
                        query.OrderByDescending(o => o.CreationTime)
                             .PageBy(input.SkipCount, input.MaxResultCount)
                    );

                var count = await _asyncQueryableExecuter.CountAsync(query);

                var resultDto = ObjectMapper.Map<List<GoodsReceivedDto>>(result.Result);
                return new PagedResultDto<GoodsReceivedDto>(count, resultDto);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        #endregion
    }
}
