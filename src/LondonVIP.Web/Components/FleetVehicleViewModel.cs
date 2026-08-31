namespace LondonVIP.Web.Components;
public sealed record FleetVehicleViewModel(string Slug,string Name,string ShortName,string Category,string Description,string HeroImage,int Passengers,int Suitcases,int CarryOns,string ExecutiveRating,IReadOnlyList<string> Features,IReadOnlyList<string> Gallery,bool AirportSuitable,bool WeddingSuitable);
public static class PublicFleetCatalog
{
 public static readonly IReadOnlyList<FleetVehicleViewModel> All=[
 new("executive-saloon","Executive Saloon","ES","Executive","Quiet, composed travel for airport and business journeys.","/media/fleet/executive-saloon/hero.jpg",4,2,2,"Excellent",["WiFi","Phone charging","Child seats on request","Meet & Greet"],Gallery("executive-saloon"),true,true),
 new("luxury-saloon","Luxury Saloon","LS","First Class","Flagship comfort with generous rear-seat refinement.","/media/fleet/luxury-saloon/hero.jpg",3,2,2,"Exceptional",["WiFi","Rear-seat comfort","Phone charging","Meet & Greet"],Gallery("luxury-saloon"),true,true),
 new("estate","Executive Estate","EE","Versatile","Executive comfort with considered luggage capacity.","/media/fleet/estate/hero.jpg",4,4,2,"Excellent",["Large boot","WiFi","Phone charging","Child seats on request"],Gallery("estate"),true,true),
 new("mpv","Luxury MPV","MPV","Group travel","Flexible premium space for families, teams and airport groups.","/media/fleet/mpv/hero.jpg",6,4,3,"Excellent",["Flexible seating","WiFi","Phone charging","Meet & Greet"],Gallery("mpv"),true,true),
 new("8-seater","Eight Seater","8S","Large groups","Generous capacity without compromising a calm journey.","/media/fleet/8-seater/hero.jpg",8,8,4,"Very good",["Eight seats","Large luggage area","Child seats on request","Airport suitable"],Gallery("8-seater"),true,false),
 new("wheelchair","Accessible Vehicle","AV","Accessible","A future-ready accessible category designed around dignity and space.","/media/fleet/wheelchair/hero.jpg",4,3,2,"Very good",["Wheelchair provision","Assisted boarding","Flexible seating","Airport suitable"],Gallery("wheelchair"),true,false),
 new("electric","Electric Executive","EV","Electric","Smooth, quiet and locally emission-free executive travel.","/media/fleet/electric/hero.jpg",4,2,2,"Excellent",["Electric drive","Phone charging","Quiet cabin","Airport suitable"],Gallery("electric"),true,true)];
 private static string[] Gallery(string slug)=>new[]{"front.jpg","rear.jpg","interior.jpg","boot.jpg","airport.jpg","night.jpg","360-01.jpg","360-02.jpg"}.Select(x=>$"/media/fleet/{slug}/{x}").ToArray();
}
