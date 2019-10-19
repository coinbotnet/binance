using AutoMapper;
using Coinbot.Domain.Contracts.Models.StockApiService;
using Coinbot.Binance.Models;

namespace Coinbot.Binance.Mappings
{
    public class BinanceProfile : Profile
    {
        public BinanceProfile()
        {
            CreateMap<TickDTOResult, Tick>()
                .ForMember(dest => dest.Ask, opt => opt.MapFrom(src => src.price))
                .ForMember(dest => dest.Bid, opt => opt.MapFrom(src => src.price))
                .ForMember(dest => dest.Last, opt => opt.MapFrom(src => src.price));

            CreateMap<TransactionDTO, Transaction>()
                .ForMember(dest => dest.OrderRefId, opt => opt.MapFrom(src => src.clientOrderId))
                .ForMember(dest => dest.IsOpen, opt => opt.MapFrom(src => src.status != "FILLED"))
                .ForMember(dest => dest.Quantity, opt => opt.MapFrom(src => src.origQty));

            CreateMap<TransactionMadeDTO, Transaction>()
                .ForMember(dest => dest.OrderRefId, opt => opt.MapFrom(src => src.clientOrderId))
                .ForMember(dest => dest.IsOpen, opt => opt.MapFrom(src => src.status != "FILLED"))
                .ForMember(dest => dest.Quantity, opt => opt.MapFrom(src => src.origQty));


        }
    }
}