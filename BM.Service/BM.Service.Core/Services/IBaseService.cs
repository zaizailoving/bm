
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using BM.Service.Core.DI;
using BM.Service.Core.Models;
namespace BM.Service.Core.Services
{

    public interface IBaseService<TEntity> : IDependency where TEntity : BaseModel
    {
        
    }
}
