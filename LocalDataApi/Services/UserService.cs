using LocalDataApi.Data;
using LocalDataApi.Models;
using Microsoft.EntityFrameworkCore;
namespace LocalDataApi.Services
{
    public class UserService
    {
        private readonly AppDbContext _context;
        public UserService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<User>> GetAllUsers()
        {
            return await _context.Users.ToListAsync();
        }


        public async Task<User?> GetUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            return user;
        }


        public async Task<User> CreateUser(User user)
        {
            user.CreateDate = DateTime.Now;
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task UpdateUser(User user)
        {
            var currentUser = await _context.Users.FindAsync(user.Id);
            if (currentUser == null)
            {
                return;
            }

            _context.Entry(currentUser).SetValuesIgnoreNullWithCollections(user);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException e)
            {
                if (!UserExists(user.Id))
                {
                    return;
                }
                else
                {
                    throw;
                }
            }

            return;
        }


        public async Task DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return;
            }
            else
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
            }

            return;
        }

        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.Id == id);
        }
    }
}
