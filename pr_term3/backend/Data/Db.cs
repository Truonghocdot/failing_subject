using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace backend.Data;

/// <summary>Nhận diện lỗi MySQL cần đổi thành 409 thay vì 500.</summary>
public static class Db
{
    /// <summary>ER_DUP_ENTRY – vi phạm unique / khoá chính (VD 2 đặt phòng cùng chèn 1 dòng room_nights).</summary>
    public static bool IsDuplicateKey(DbUpdateException ex) => ex.InnerException is MySqlException { Number: 1062 };

    /// <summary>
    /// Trùng khoá hoặc 2 transaction chèn chồng đêm khoá nhau (ER_LOCK_DEADLOCK 1213 / ER_LOCK_WAIT_TIMEOUT 1205):
    /// MySQL huỷ 1 bên – với người dùng đều là "phòng vừa có người đặt".
    /// </summary>
    public static bool IsNightConflict(Exception ex)
    {
        var my = ex as MySqlException ?? ex.InnerException as MySqlException;
        return my?.Number is 1062 or 1213 or 1205;
    }
}
