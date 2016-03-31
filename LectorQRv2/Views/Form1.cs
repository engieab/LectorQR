// mi progragama principal

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using AForge.Video;
using AForge.Video.DirectShow;
using BarcodeLib.BarcodeReader;
using Excel = Microsoft.Office.Interop.Excel;
using System.Reflection;
using System.IO.Ports;
using System.IO;

namespace LectorQRv2.Views
{
    public partial class Form1 : Form
    {
    	 SerialPort port;
	 string comPort;
    	
        public Form1()
        {
           try
            {
                InitializeComponent();
                comPort = "COM5"; //the comport the Arduino is connected to... you will have to change this most likely
                port = new SerialPort(comPort, 9600, Parity.None, 8, StopBits.One);
                port.DtrEnable = true;
                port.Open();

            }
            catch (Exception /*ex*/)
            {
                textBox1.Text = "Can't Open COM5"; 
            }
        }
        
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (port.IsOpen)
                port.Close();
        }

        private FilterInfoCollection Dispositivos;
        private VideoCaptureDevice FuenteDeVideo;
        private Core.ParqueoFlow ControlParqueo = new Core.ParqueoFlow();

        private void Form1_Load(object sender, EventArgs e)
        {/*
            Dispositivos = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            foreach (FilterInfo x in Dispositivos) 
            {
                comboBox1.Items.Add(x.Name);
            }
            comboBox1.SelectedIndex = 0;*/
        }

        private void button1_Click(object sender, EventArgs e)
        {
            timer1.Enabled = true;
            FuenteDeVideo = new VideoCaptureDevice(Dispositivos[comboBox1.SelectedIndex].MonikerString);
            videoSourcePlayer1.VideoSource = FuenteDeVideo;
            //INICIAR RECEPCION DE IMAGENES
            videoSourcePlayer1.Start();

        }

        private void button2_Click(object sender, EventArgs e)
        {
            new MATLAB.Execute().run();
            //timer1.Enabled = false;
            //videoSourcePlayer1.SignalToStop();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            //ESTAR SEGUROS QUE HAY UNA IMAGEN DESDE LA WEBCAM
            if (videoSourcePlayer1.GetCurrentVideoFrame() != null)
            {
                //IBTENER IMAGEN DE LA WEBCAM
                Bitmap img = new Bitmap(videoSourcePlayer1.GetCurrentVideoFrame());
                //UTILIZAR LA LIBRERIA Y LEER EL CÓDIGO
                string[] resultados = BarcodeReader.read(img, BarcodeReader.QRCODE);
                //QUITAR LA IMAGEN DE MEMORIA
                img.Dispose();
                //OBTENER LAS LECTURAS CUANDO SE LEA ALGO
                if (resultados != null && resultados.Count() > 0)
                {
                    SaveQR(resultados[0].Substring(1));
                    
                   
                }

                resultados = null;
                    
                }
            }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
        private void SaveQR(String qrcode)
        { 
            Models.Parqueo parqueo = new Models.Parqueo
            {
                cedula = qrcode,
                fecha_salida = null,
                fecha_entrada = DateTime.Now,
                placa = "PENDIENTE"
            };

            DAO.Repository<Models.Parqueo> ParqueoDAO = new DAO.Repository<Models.Parqueo>();
            if (ParqueoDAO.SelectSingle(p => p.cedula == qrcode && p.placa == "PENDIENTE") != null)
            {
                MessageBox.Show(this, "Cedula con placa pendiente");
                return;
            }

            ParqueoDAO.Insert(parqueo);
            ParqueoDAO.SaveAll();
            MessageBox.Show(this, "Se graba la vaina!");
        }

        // codigo para agregar manual la placa y Qr

        private void btnCedula1_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtCedula1.Text))
            {
                MessageBox.Show(this, "Campo vacío!");
                return;
            }

            try
            {
                ControlParqueo.EntradaInsertarQR(txtCedula1.Text);
            }
            catch (Core.PlacaPendienteException ex)
            {
                MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            MessageBox.Show(this, "Se inserta QR exitosamente");
        }

        private void btnPlaca1_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtPlaca1.Text))
            {
                MessageBox.Show(this, "Campo vacío!");
                return;
            }

            try
            {
                ControlParqueo.EntradaInsertarPlaca(new Models.Placa(txtPlaca1.Text));
            }
            catch (Models.InvalidPlacaException ipe)
            {
                MessageBox.Show(this, ipe.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            catch (Core.PlacaNoPendienteException ex) // si no hay Qr con placa pendiente no puedo agrega una placa
            {
                MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            MessageBox.Show(this, "Se inserta placa exitosamente");
             port.Write("1"); // Abre la puerta de Entrada
        }

        private void btnCedula2_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtCedula2.Text))
            {
                MessageBox.Show(this, "Campo vacío!");
                return;
            }

            if (ControlParqueo.SalidaInsertarQR(txtCedula2.Text).Count == 0)
            {
                MessageBox.Show(this, "Ningún parqueo asociado con este QR!");
                return;
            }

            MessageBox.Show(this, "Se llaman los registros asociados con el QR exitosamente");
        }

        private void btnPlaca2_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtPlaca2.Text))
            {
                MessageBox.Show(this, "Campo vacío!");
                return;
            }

            try
            {
                Models.Parqueo p = ControlParqueo.SalidaInsertarPlaca(new Models.Placa(txtPlaca2.Text));
                if (p == null)
                {
                    MessageBox.Show(this, "QR no coincide con la placa!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                ControlParqueo.ConfirmarSalida(p);
                MessageBox.Show(this, "Salida exitosa!");
                 port.Write("0"); // Abre la puerta de Salida
            }
            catch (Models.InvalidPlacaException ipe)
            {
                MessageBox.Show(this, ipe.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }
    }
}

	             
                
        

        
        
    

