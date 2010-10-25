﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Deveel.Data.Net {
	public sealed class FileSystemBlockService : BlockService {
		private readonly string path;
		
		private bool compressFinished;
		private bool hasCompressFinished;
		private readonly List<BlockContainer> compressionAddList = new List<BlockContainer>();
		private readonly object compressLock = new object();
		private readonly Thread compressionThread;
		
		private Timer fileDeleteTimer;
		
		public FileSystemBlockService(IServiceConnector connector, string path)
			: base(connector) {
			this.path = path;
			compressionThread = new Thread(Compress);
			compressionThread.IsBackground = true;
		}
		
		private void FileDelete(object state) {
			string fileName = (string)state;
			
			//TODO: INFO log ...
			
			File.Delete(fileName);
		}
		
		private void Compress() {
			try {
				lock(compressLock) {
					// Wait 2 seconds,
					Monitor.Wait(compressLock, 2000);

					// Any new block containers added, we need to process,
					List<BlockContainer> new_items = new List<BlockContainer>();
					while (true) {
						lock(compressionAddList) {
							foreach(BlockContainer container in compressionAddList)
								new_items.Add(container);
							compressionAddList.Clear();
						}

						// Sort the container list,
						new_items.Sort();

						for (int i = new_items.Count - 1; i >= 0; i--) {
							BlockContainer container = new_items[i];

							// If it's already compressed, remove it from the list
							if (container.BlockStore is CompressedBlockStore) {
								new_items.RemoveAt(i);
							} else if (container.LastWriteTime <
							           DateTime.Now.AddMilliseconds(-(3*60*1000))) {

								FileBlockStore mblock_store = (FileBlockStore) container.BlockStore;
								string sourcef = mblock_store.FileName;
								string destf = Path.Combine(Path.GetDirectoryName(sourcef),
									                        Path.GetFileNameWithoutExtension(sourcef) + ".tempc");
								try {
									File.Delete(destf);
									
									//TODO: INFO log ...

									// Compress the file,
									CompressedBlockStore.Compress(sourcef, destf);
									// Rename the file,
									string compressedf = Path.Combine(Path.GetDirectoryName(sourcef),
									                                  Path.GetFileNameWithoutExtension(sourcef) + ".mcd");
									File.Move(destf, compressedf);

									// Switch the block container,
									container.ChangeStore(new CompressedBlockStore(container.BlockId, compressedf));

									//TODO: INFO log ...
									
									// Wait a little bit and delete the original file,
									if (compressFinished) {
										hasCompressFinished = true;
										Monitor.PulseAll(compressLock);
										return;
									}
									
									Monitor.Wait(compressLock, 1000);

									// Delete the file after 5 minutes,
									fileDeleteTimer = new Timer(FileDelete, sourcef, 5*60*1000, 5*60*1000);

									// Remove it from the new_items list
									new_items.RemoveAt(i);
								} catch(IOException e) {
									Logger.Error("IO Error while compressing", e);
								}
							}

							if (compressFinished) {
								hasCompressFinished = true;
								Monitor.PulseAll(compressLock);
								return;
							}
							Monitor.Wait(compressLock, 200);

						}

						if (compressFinished) {
							hasCompressFinished = true;
							Monitor.PulseAll(compressLock);
							return;
						}
						Monitor.Wait(compressLock, 3000);
					}
				}
			} catch(ThreadInterruptedException) {
				// ThreadInterruptedException causes the thread to end,
			}
		}
		
		private void FinishCompress() {
			lock (compressLock) {
				compressFinished = true;
				while (!hasCompressFinished) {
					try {
						Monitor.PulseAll(compressLock);
						Monitor.Wait(compressLock);
					} catch (ThreadInterruptedException) {
					}
				}
			}
		}
		
		protected override void OnStart() {
			// Open the guid file,
			string guidFile = Path.Combine(path, "block_server_guid");
			// If the guid file exists,
			if (File.Exists(guidFile)) {
				// Get the contents,
				using(StreamReader reader = new StreamReader(guidFile)) {
					string line = reader.ReadLine();
					// Set the service guid
					SetGuid(Int64.Parse(line.Trim()));
				}
			} else {
				// The guid file doesn't exist, so create one now,
				Stream fileStream;
				try {
					fileStream = File.Create(guidFile);
				} catch (Exception e) {
					throw new ApplicationException("Unable to create guid service file", e);
				}

				// Create a unique server_guid
				Random r = new Random();
				int v1 = r.Next();
				long v2 = DateTime.Now.Ticks;
				long guid = (v2 << 16) ^ (v1 & 0x0FFFFFFF);

				// Write it out to the guid file,
				using (StreamWriter writer = new StreamWriter(fileStream)) {
					writer.WriteLine(guid);
					writer.Flush();
				}
				
				SetGuid(guid);
			}
			
			compressionThread.Start();

			base.OnStart();
		}
		
		protected override BlockContainer LoadBlock(long blockId) {
			// If it's not found in the map,
			// Turn the block id into a filename,
			String block_fname = blockId.ToString();
			string block_file_name = Path.Combine(path, block_fname + ".mcd");
			IBlockStore block_store;
			if (!File.Exists(block_file_name)) {
				block_file_name = Path.Combine(path, block_fname);
				// If this file doesn't exist,
				if (!File.Exists(block_file_name)) {
					// We check if the block_id is less than maximum id. If it is we
					// generate an exception indicating this block doesn't exist on this
					// service. This means something screwed up, either the manager service
					// was erroneously told the block was located on this service but it
					// isn't, or the file was deleted by the user.
					if (blockId < LastBlockId)
						throw new BlockReadException("Block " + blockId + " not stored on service");
				}

				block_store = new FileBlockStore(blockId, block_file_name);
			} else {
				block_store = new CompressedBlockStore(blockId, block_file_name);
			}

			// Make the block container object,
			BlockContainer container = new BlockContainer(blockId, block_store);

			// Add the new container to the control list (used by the compression
			// thread).
			lock (compressionAddList) {
				compressionAddList.Add(container);
			}

			return container;
		}
		
		protected override long[] ListBlocks() {
			string[] dir = Directory.GetFiles(path);
			List<long> blocks = new List<long>(dir.Length);
			foreach (string f in dir) {
				string fileName = Path.GetFileName(f);
				if (!fileName.Equals("block_server_guid") &&
					!fileName.EndsWith(".tempc") &&
					!fileName.EndsWith(".tmpc1") &&
					!fileName.EndsWith(".tmpc2")) {
					if (fileName.EndsWith(".mcd")) {
						fileName = fileName.Substring(0, fileName.Length - 4);
					}
					long block_id = Int64.Parse(fileName);
					blocks.Add(block_id);
				}
			}

			return blocks.ToArray();
		}
		
		protected override void OnCompleteBlockWrite(long blockId, int storeType) {
			String tmpext;
			if (storeType == 1) {
				tmpext = ".tmpc1";
			} else if (storeType == 2) {
				tmpext = ".tmpc2";
			} else {
				throw new ApplicationException("Unknown file_type: " + storeType);
			}

			// Make sure this process is exclusive
			lock (BlockUploadSyncRoot) {
				string block_fname = blockId.ToString();
				string f = Path.Combine(path, block_fname + tmpext);

				if (!File.Exists(f))
					throw new ApplicationException("File doesn't exist");

				// Check the file we are renaming to doesn't exist,
				string f_normal = Path.Combine(path, block_fname);
				string f_compress = Path.Combine(path, block_fname + ".mcd");

				if (File.Exists(f_normal) || File.Exists(f_compress))
					throw new ApplicationException("Block file exists already");

				// Does exist and is a file,
				// What we will rename the file to,
				BlockContainer container = GetBlock(blockId);
				container.BlockStore.Close();
				
				if (storeType == 1) {
					File.Move(f, f_normal);
				} else if (storeType == 2) {
					File.Move(f, f_compress);
				} else {
					throw new ApplicationException();
				}
			}
		}
		
		protected override void OnWriteBlockPart(long blockId, long pos, int storeType, byte[] buffer, int length) {
			String tmpext;
			if (storeType == 1) {
				tmpext = ".tmpc1";
			} else if (storeType == 2) {
				tmpext = ".tmpc2";
			} else {
				throw new ApplicationException("Unknown file_type: " + storeType);
			}

			// Make sure this process is exclusive
			lock (BlockUploadSyncRoot) {
				try {
					string f = Path.Combine(path, blockId + tmpext);
					if (pos == 0) {
						if (File.Exists(f))
							throw new ApplicationException("File '" + f + "' already exists.");
						
						File.Create(f);
					}

					if (new FileInfo(f).Length != pos)
						throw new ApplicationException("Block sync issue on block file.");

					// Everything ok, we can write the file,
					using (FileStream fout = new FileStream(f, FileMode.Append, FileAccess.Write)) {
						fout.Write(buffer, 0, length);
					}

				} catch (IOException e) {
					throw new ApplicationException("IO Error: " + e.Message);
				}
			}
		}

		protected override void OnStop() {
			FinishCompress();

			if (fileDeleteTimer != null)
				fileDeleteTimer.Dispose();

			base.OnStop();
		}
	}
}