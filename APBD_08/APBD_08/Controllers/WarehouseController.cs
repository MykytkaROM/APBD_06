using System.Data;
using APBD_08.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace APBD_08.Controllers;

[Route("api/[controller]")]
[ApiController]
public class WarehouseController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public WarehouseController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpPost("add-product")]
    public IActionResult AddProductToWarehouse([FromBody] WarehouseRequest request)
    {
        using (var connection = new SqlConnection(_configuration.GetConnectionString("WarehouseDB")))
        {
            connection.Open();
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    
                    var checkProduct = new SqlCommand("SELECT 1 FROM Product WHERE IdProduct = @IdProduct", connection,
                        transaction);
                    checkProduct.Parameters.AddWithValue("@IdProduct", request.IdProduct);
                    if (checkProduct.ExecuteScalar() == null)
                        return NotFound("Product not found");

                   
                    var checkWarehouse = new SqlCommand("SELECT 1 FROM Warehouse WHERE IdWarehouse = @IdWarehouse",
                        connection, transaction);
                    checkWarehouse.Parameters.AddWithValue("@IdWarehouse", request.IdWarehouse);
                    if (checkWarehouse.ExecuteScalar() == null)
                        return NotFound("Warehouse not found");

                   
                    if (request.Amount <= 0)
                        return BadRequest("Amount must be greater than zero");

                   
                    var checkOrderCmd = new SqlCommand(@"
                        SELECT TOP 1 IdOrder 
                        FROM [Order] 
                        WHERE IdProduct = @IdProduct AND Amount = @Amount AND CreatedAt < @CreatedAt", connection,
                        transaction);
                    checkOrderCmd.Parameters.AddWithValue("@IdProduct", request.IdProduct);
                    checkOrderCmd.Parameters.AddWithValue("@Amount", request.Amount);
                    checkOrderCmd.Parameters.AddWithValue("@CreatedAt", request.CreatedAt);

                    var idOrder = checkOrderCmd.ExecuteScalar();
                    if (idOrder == null)
                        return NotFound("Matching order not found");

                   
                    var checkFulfillment = new SqlCommand("SELECT 1 FROM Product_Warehouse WHERE IdOrder = @IdOrder",
                        connection, transaction);
                    checkFulfillment.Parameters.AddWithValue("@IdOrder", (int)idOrder);
                    if (checkFulfillment.ExecuteScalar() != null)
                        return Conflict("Order already fulfilled");

                    
                    var updateOrder =
                        new SqlCommand("UPDATE [Order] SET FulfilledAt = GETDATE() WHERE IdOrder = @IdOrder",
                            connection, transaction);
                    updateOrder.Parameters.AddWithValue("@IdOrder", (int)idOrder);
                    updateOrder.ExecuteNonQuery();

                   
                    var priceCmd = new SqlCommand("SELECT Price FROM Product WHERE IdProduct = @IdProduct", connection,
                        transaction);
                    priceCmd.Parameters.AddWithValue("@IdProduct", request.IdProduct);
                    var price = (decimal)priceCmd.ExecuteScalar();

                    
                    var insertCmd = new SqlCommand(@"
                        INSERT INTO Product_Warehouse (IdWarehouse, IdProduct, IdOrder, Amount, Price, CreatedAt)
                        VALUES (@IdWarehouse, @IdProduct, @IdOrder, @Amount, @Price, GETDATE());
                        SELECT SCOPE_IDENTITY();", connection, transaction);
                    insertCmd.Parameters.AddWithValue("@IdWarehouse", request.IdWarehouse);
                    insertCmd.Parameters.AddWithValue("@IdProduct", request.IdProduct);
                    insertCmd.Parameters.AddWithValue("@IdOrder", (int)idOrder);
                    insertCmd.Parameters.AddWithValue("@Amount", request.Amount);
                    insertCmd.Parameters.AddWithValue("@Price", price * request.Amount);

                    var newId = insertCmd.ExecuteScalar();

                    transaction.Commit();
                    return Ok(new { Id = newId });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return StatusCode(500, ex.Message);
                }
            }
        }
    }
    [HttpPost("add-product-proc")]
    public IActionResult AddProductUsingProc([FromBody] WarehouseRequest request)
    {
        using (var connection = new SqlConnection(_configuration.GetConnectionString("WarehouseDB")))
        {
            connection.Open();
            try
            {
                var cmd = new SqlCommand("AddProductToWarehouse", connection);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@IdProduct", request.IdProduct);
                cmd.Parameters.AddWithValue("@IdWarehouse", request.IdWarehouse);
                cmd.Parameters.AddWithValue("@Amount", request.Amount);
                cmd.Parameters.AddWithValue("@CreatedAt", request.CreatedAt);

                var result = cmd.ExecuteScalar();

                if (result == null)
                    return StatusCode(500, "Operation failed");

                return Ok(new { Id = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}