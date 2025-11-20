<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

	<xsl:template match="/">

		<style type="text/css">
			span.text{
				color:black;
			}
			span.attribute{
				color:#993600;
			}
			span.tag{
				padding-left:0px;
				color:#000899;
			}</style>
		<div style="font-size: 14px;">
			<xsl:apply-templates/>
		</div>
	</xsl:template>

	<xsl:template match="*">
		<div style="padding-left: {8 * count(ancestor::*)}px;">
			<xsl:choose>
				<xsl:when test="not(local-name(.)='br')">
					<span>
						
						<!-- opening tag -->
						<span class="tag">
							<xsl:text>&lt;</xsl:text>
							<xsl:value-of select="name()"/>
							<xsl:apply-templates select="@*"/>
							<xsl:text>&gt;</xsl:text>
						</span>
						
						<!-- content -->
						<xsl:choose>
							<xsl:when test="*">
								<span style="padding-left: 2px; color: black;">
									<xsl:apply-templates select="* | text()"/>
								</span>
							</xsl:when>
							<xsl:otherwise>
								<span class="text">
									<xsl:apply-templates select="node()"/>
								</span>			
							</xsl:otherwise>
						</xsl:choose>
						
						<!-- closing tag -->
						<span class="tag">
							<xsl:text>&lt;/</xsl:text>
							<xsl:value-of select="name()"/>
							<xsl:text>&gt;</xsl:text>
						</span>
					</span>
				</xsl:when>
				<xsl:otherwise>
					<span class="tag">
						<xsl:text>&lt;</xsl:text>
						<xsl:value-of select="name()"/>
						<xsl:apply-templates select="@*"/>
						<xsl:text>/&gt;</xsl:text>
					</span>
				</xsl:otherwise>
			</xsl:choose>
		</div>
	</xsl:template>
	
	<xsl:template match="@*">
		<xsl:text> </xsl:text>
		<span class="attribute">
			<xsl:value-of select="name()"/>
			<xsl:text>="</xsl:text>
		</span>
		<span class="text">
			<xsl:value-of select="."/>
		</span>
		<span class="attribute">
			<xsl:text>"</xsl:text>
		</span>
	</xsl:template>
	

</xsl:stylesheet>
