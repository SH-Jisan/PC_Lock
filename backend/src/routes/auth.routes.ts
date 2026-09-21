import { Router } from 'express';
import { v4 as uuidv4 } from 'uuid';
import jwt from 'jsonwebtoken';
import { getDb } from '../db';

const JWT_SECRET = process.env.JWT_SECRET || 'pc_security_master_jwt_secret_2026';

export function createAuthRouter(): Router {
  const router = Router();

  router.post('/login', async (req, res) => {
    const { email } = req.body;
    const db = await getDb();
    let user = await db.get('SELECT * FROM users WHERE email = ?', [email]);

    if (!user) {
      const userId = uuidv4();
      await db.run('INSERT INTO users (id, email, password_hash) VALUES (?, ?, ?)', [userId, email, 'hash_placeholder']);
      user = { id: userId, email };
    }

    const token = jwt.sign({ userId: user.id, email: user.email }, JWT_SECRET, { expiresIn: '7d' });
    res.json({ status: 'SUCCESS', token, userId: user.id });
  });

  return router;
}
