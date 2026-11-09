try {
  const response = await fetch(`http://127.0.0.1:${process.env.PORT || '3000'}/`, { signal: AbortSignal.timeout(5000), redirect: 'manual' });
  process.exitCode = response.status === 200 ? 0 : 1;
  await response.body?.cancel();
} catch { process.exitCode = 1; }
