using MassTransit;
using WindDataReceiver.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var rabbitConfig = builder.Configuration.GetSection("RabbitMQ");

builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(
            rabbitConfig["HostName"],
            h =>
            {
                h.Username(rabbitConfig["UserName"]);
                h.Password(rabbitConfig["Password"]);
            });
    });
});

builder.Services.AddHostedService<ComPortWorker>();
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
