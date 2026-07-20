
using Mapster;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using BM.Service.Core.Models;
using Microsoft.Extensions.Localization;

namespace BM.Service.Core.Services
{
    public class BaseService<TEntity> : IBaseService<TEntity> where TEntity : BaseModel
    {

    }
}
