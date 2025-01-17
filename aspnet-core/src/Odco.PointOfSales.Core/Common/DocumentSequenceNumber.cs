﻿using Abp.Domain.Entities.Auditing;
using Odco.PointOfSales.Core.Enums;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Odco.PointOfSales.Core.Common
{
    [Table("Common.DocumentSequenceNumber")]
    public class DocumentSequenceNumber : FullAuditedEntity<Guid>
    {
        [Required]
        public DocumentType DocumentType { get; set; }

        [StringLength(25)]
        public string Description { get; set; }

        /// <summary>
        /// See
        /// </summary>
        [StringLength(5)]
        public string Prefix { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [StringLength(5)]
        public string Suffix { get; set; }

        /// <summary>
        /// 1
        /// </summary>
        [Required]
        public int StartingNumber { get; set; }

        /// <summary>
        /// Last Sequence Number
        /// 0
        /// </summary>
        [Required]
        public int LastNumber { get; set; }

        /// <summary>
        /// PO is 10 then it should be assign in here!
        /// </summary>
        [Required]
        public int Length { get; set; }

        /// <summary>
        /// True => Auto generated
        /// </summary>
        public bool IsAutoGenerated { get; set; }
    }
}
